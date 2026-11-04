using System.Text.Json;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Provides explicit browser-response faults; only DropCommitted sends through to the actual API.</summary>
internal sealed class EditorRecoveryResponse : IAsyncDisposable
{
    internal enum Behavior
    {
        FiniteStatus, HoldWithoutDispatch, DropCommitted
    }
    private const string JsonProblem = "application/problem+json";
    private const string SecretField = "clientSecret";
    private const string Cache = "Cache-Control";
    private const string NoStore = "no-store";
    private const string Sniff = "X-Content-Type-Options";
    private const string NoSniff = "nosniff";
    private const int Unavailable = 503;
    private const int Success = 200;
    private const int Created = 201;
    private const int TransportMilliseconds = 10000;
    private static readonly TimeSpan JoinBudget = TimeSpan.FromSeconds(8);
    private readonly IPage page;
    private readonly string url;
    private readonly string method;
    private readonly Behavior behavior;
    private readonly int status;
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource joined = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int Sends
    {
        get; private set;
    }
    internal bool Failed
    {
        get; private set;
    }
    internal string? SuppressedSecret
    {
        get; private set;
    }
    internal int? CommittedStatus
    {
        get; private set;
    }

    /// <summary>Fixes one exact endpoint and method; all other traffic proceeds normally.</summary>
    private EditorRecoveryResponse(IPage page, string url, string method, Behavior behavior, int status)
    {
        this.page = page;
        this.url = url;
        this.method = method;
        this.behavior = behavior;
        this.status = status;
    }

    /// <summary>Installs one removable response seam without changing the app's request timeout or retry policy.</summary>
    internal static async Task<EditorRecoveryResponse> AttachAsync(IPage page, string url, string method,
        Behavior behavior = Behavior.FiniteStatus, int status = Unavailable)
    {
        var probe = new EditorRecoveryResponse(page, url, method, behavior, status);
        await page.RouteAsync(url, probe.HandleAsync);
        return probe;
    }

    /// <summary>Joins the exact browser request before inspecting a deliberately paused working state.</summary>
    internal Task EnteredAsync() => entered.Task.WaitAsync(JoinBudget, TestContext.Current.CancellationToken);

    /// <summary>Never propagates arbitrary Playwright response bodies or exception messages into diagnostics.</summary>
    private async Task HandleAsync(IRoute route)
    {
        if (route.Request.Method != method)
        {
            await route.ContinueAsync();
            return;
        }
        Sends++;
        entered.TrySetResult();
        try
        {
            if (behavior == Behavior.HoldWithoutDispatch)
            {
                await release.Task.WaitAsync(JoinBudget, TestContext.Current.CancellationToken);
                await route.AbortAsync();
            }
            else if (behavior == Behavior.DropCommitted)
            {
                await DropAsync(route);
            }
            else
            {
                await route.FulfillAsync(new()
                {
                    Status = status,
                    ContentType = JsonProblem,
                    Body = JsonSerializer.Serialize(new
                    {
                        status
                    }),
                    Headers = new Dictionary<string, string> { [Cache] = NoStore, [Sniff] = NoSniff }
                });
            }
        }
        catch { Failed = true; try { await route.AbortAsync(); } catch { } }
        finally { joined.TrySetResult(); }
    }

    /// <summary>Completes one real mutation, retains its generated secret privately, then loses only the browser response.</summary>
    private async Task DropAsync(IRoute route)
    {
        var response = await route.FetchAsync(new()
        {
            MaxRedirects = 0,
            MaxRetries = 0,
            Timeout = TransportMilliseconds
        });
        try
        {
            CommittedStatus = response.Status;
            if (response.Status is not (Success or Created))
            {
                Failed = true;
            }
            using var body = JsonDocument.Parse(await response.BodyAsync());
            if (body.RootElement.TryGetProperty(SecretField, out var value))
            {
                SuppressedSecret = value.GetString();
            }
        }
        finally { await response.DisposeAsync(); }
        await route.AbortAsync();
    }

    /// <summary>Releases and joins interception before removing it; the caller owns subsequent explicit recovery.</summary>
    public async ValueTask DisposeAsync()
    {
        release.TrySetResult();
        if (Sends > 0)
        {
            await joined.Task.WaitAsync(JoinBudget, TestContext.Current.CancellationToken);
        }
        await page.UnrouteAsync(url, HandleAsync);
    }
}
