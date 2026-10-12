using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Serves a real no-store POST history control without request interception, credentials or browser cache overrides.</summary>
internal sealed class HistoryControlServer(WebApplication application) : IAsyncDisposable
{
    internal const string Canary = "synthetic-history-server-canary";
    internal const string Submit = "#submit";
    internal const string Secret = "#secret";
    internal const string Continue = "#continue";
    internal const string SafePath = "/safe";
    private const string PostPath = "/post";
    private const string NoStore = "no-store";
    private const string HtmlType = "text/html";
    private const string Form = "<!doctype html><html><body><form method='post' action='/post'><button id='submit'>Submit control</button></form></body></html>";
    private const string Disclosure = "<!doctype html><html><body><input id='secret' value='" + Canary + "'><a id='continue' href='/safe'>Continue</a></body></html>";
    private const string Safe = "<!doctype html><html><body>Safe control</body></html>";
    private int posts;
    internal string Origin => application.Urls.Single();
    internal int Posts => Volatile.Read(ref posts);

    /// <summary>Starts an ephemeral loopback HTTP server solely for native cache-behavior evidence; real account journeys independently require HTTPS.</summary>
    internal static async Task<HistoryControlServer> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var application = builder.Build();
        var server = new HistoryControlServer(application);
        application.Run(server.RespondAsync);
        try
        {
            await application.StartAsync(TestContext.Current.CancellationToken);
            return server;
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    /// <summary>Discloses only a synthetic marker once; any actual POST resubmission receives a safe document and increments independent server evidence.</summary>
    private async Task RespondAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = NoStore;
        context.Response.ContentType = HtmlType;
        var body = context.Request.Path == SafePath ? Safe : Form;
        if (context.Request.Path == PostPath && HttpMethods.IsPost(context.Request.Method))
        {
            body = Interlocked.Increment(ref posts) == 1 ? Disclosure : Safe;
        }
        await context.Response.WriteAsync(body, context.RequestAborted);
    }

    /// <summary>Stops and disposes the test-owned server without leaving a listener beyond its control case.</summary>
    public ValueTask DisposeAsync() => application.DisposeAsync();
}
