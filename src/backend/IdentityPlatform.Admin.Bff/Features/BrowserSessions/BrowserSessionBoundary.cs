using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Owns a bounded, nonrenewing status response after all storage cleanup, without automatic authentication redirects.</summary>
internal static class BrowserSessionBoundary
{
    internal const string Path = "/bff/session";
    private const string NoStore = "no-store";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string NoReferrer = "no-referrer";

    /// <summary>Rejects non-status framing and publishes no identity when storage, cancellation or cleanup fails.</summary>
    internal static async Task RunAsync(HttpContext http, IServiceScopeFactory factory, TimeProvider clock)
    {
        var original = http.RequestAborted;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(original);
        cancellation.CancelAfter(ProtocolStorageLifetime.RequestLimit);
        http.RequestAborted = cancellation.Token;
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers[ReferrerPolicy] = NoReferrer;
        try
        {
            var budget = new ProtocolStorageRequest(clock, cancellation.Token);
            if (!BrowserLoginRequest.Get(http.Request) || http.Request.ContentType is not null ||
                http.Request.Query.Count != 0 || !await BrowserLoginRequest.EmptyAsync(http))
            {
                http.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            var read = await ReadAsync(http, factory);
            budget.RequireCurrent();
            await Results.Json(read?.Status(clock.GetUtcNow()) ?? new BrowserSessionStatus(false)).ExecuteAsync(http);
        }
        catch
        {
            if (http.Response.HasStarted || original.IsCancellationRequested)
            {
                http.Abort();
            }
            else
            {
                http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
        }
        finally { http.RequestAborted = original; }
    }

    /// <summary>Disposes the actual scoped reader before its detached result can supply response identity.</summary>
    private static async Task<BrowserSessionRead?> ReadAsync(HttpContext http, IServiceScopeFactory factory)
    {
        await using var scope = factory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BrowserSessionReader>().ReadAsync(http);
    }
}
