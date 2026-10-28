using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Owns synthetic loopback HTTP and an actual browser; no identity, credential, certificate override or database is involved.</summary>
internal sealed class LocalDemoNavigationFixture : IAsyncDisposable
{
    internal const string FormPath = "/form";
    internal const string DisabledPath = "/disabled";
    internal const string HangingPath = "/hang";
    internal const string SubmitSelector = "#submit";
    internal const string CompleteSelector = "#complete";
    private const string SubmitPath = "/submit";
    private const string NeverPath = "/never";
    private const string HopPattern = "/hop/{step:int}";
    private const string HopPrefix = "/hop/";
    private const string StepField = "step";
    private const string CompletePath = "/complete";
    private const string Html = "text/html; charset=utf-8";
    private const string Form = "<!doctype html><form method='post' action='/submit'><button id='submit'>Continue</button></form>";
    private const string DisabledForm = "<!doctype html><form method='post' action='/submit'><button id='submit' disabled>Continue</button></form>";
    private const string HangingForm = "<!doctype html><form method='post' action='/never'><button id='submit'>Continue</button></form>";
    private const string Complete = "<!doctype html><p id='complete'>Complete</p>";
    private const int LastHop = 3;
    private static readonly TimeSpan HopDelay = TimeSpan.FromMilliseconds(4250);
    private readonly WebApplication application;
    private IPlaywright? playwright;
    private IBrowser? browser;
    private int submissions;
    internal int Submissions => Volatile.Read(ref submissions);
    internal string Destination => new Uri(new Uri(application.Urls.Single()), CompletePath).AbsoluteUri;

    /// <summary>Retains the actual server owner before startup so partial acquisition can be disposed.</summary>
    private LocalDemoNavigationFixture(WebApplication application) => this.application = application;

    /// <summary>Launches the installed CI browser by default; an explicit executable supports separately labelled local investigation.</summary>
    internal static async Task<LocalDemoNavigationFixture> CreateAsync(string? executablePath = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var owner = new LocalDemoNavigationFixture(builder.Build());
        owner.Map();
        try
        {
            await owner.application.StartAsync();
            owner.playwright = await Playwright.CreateAsync();
            owner.browser = await owner.playwright.Chromium.LaunchAsync(new()
            {
                ExecutablePath = executablePath
            });
            return owner;
        }
        catch { await owner.DisposeAsync(); throw; }
    }

    /// <summary>Maps only fixed synthetic forms and four independently bounded redirect responses.</summary>
    private void Map()
    {
        application.MapGet(FormPath, context => WriteAsync(context, Form));
        application.MapGet(DisabledPath, context => WriteAsync(context, DisabledForm));
        application.MapGet(HangingPath, context => WriteAsync(context, HangingForm));
        application.MapGet(CompletePath, context => WriteAsync(context, Complete));
        application.MapPost(SubmitPath, context =>
        {
            Interlocked.Increment(ref submissions);
            Redirect(context, HopPrefix + 0);
            return Task.CompletedTask;
        });
        application.MapPost(NeverPath, async context =>
        {
            Interlocked.Increment(ref submissions);
            await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
        });
        application.MapGet(HopPattern, HopAsync);
    }

    /// <summary>Delays four responses for a total of seventeen seconds, while each individual synthetic hop stays below five seconds.</summary>
    private static async Task HopAsync(HttpContext context)
    {
        var step = int.Parse((string)context.Request.RouteValues[StepField]!, CultureInfo.InvariantCulture);
        if (step is < 0 or > LastHop)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        await Task.Delay(HopDelay, context.RequestAborted);
        Redirect(context, step == LastHop ? CompletePath : HopPrefix + (step + 1).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Requires browser-native POST-to-GET navigation through a standard see-other response.</summary>
    private static void Redirect(HttpContext context, string location)
    {
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = location;
    }

    /// <summary>Serves only fixed public fixture markup, never application or browser state.</summary>
    private static Task WriteAsync(HttpContext context, string html)
    {
        context.Response.ContentType = Html;
        return context.Response.WriteAsync(html, context.RequestAborted);
    }

    /// <summary>Creates an isolated cookie-free context using exactly the normal demo's existing defaults.</summary>
    internal async Task<IBrowserContext> ContextAsync()
    {
        var context = await browser!.NewContextAsync();
        LocalDemoNavigation.Configure(context);
        return context;
    }

    /// <summary>Opens a fixed synthetic form without altering browser actionability or navigation behavior.</summary>
    internal Task<IResponse?> OpenAsync(IPage page, string path) => page.GotoAsync(new Uri(new Uri(application.Urls.Single()), path).AbsoluteUri);

    /// <summary>Closes browser connections before the cancellable server requests and runtime owners.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (browser is not null)
            {
                await browser.DisposeAsync();
            }
        }
        finally
        {
            playwright?.Dispose();
            await application.DisposeAsync();
        }
    }
}
