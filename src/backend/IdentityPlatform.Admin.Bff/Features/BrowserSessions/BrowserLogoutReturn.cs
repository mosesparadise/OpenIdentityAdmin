using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Consumes only a protected one-use logout return; never establishes authority or modifies later session cookies.</summary>
internal static class BrowserLogoutReturn
{
    private const string NoStore = "no-store";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string NoReferrer = "no-referrer";
    /// <summary>Keeps one original request deadline and withholds all outcome material until scope disposal and original proof checks complete.</summary>
    internal static async Task RunAsync(HttpContext http, IServiceScopeFactory factory, TimeProvider clock)
    {
        var services = http.RequestServices;
        var cancellation = http.RequestAborted;
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        bounded.CancelAfter(ProtocolStorageLifetime.RequestLimit);
        var deadline = new ProtocolStorageRequest(clock, bounded.Token);
        http.RequestAborted = bounded.Token;
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers[ReferrerPolicy] = NoReferrer;
        try
        {
            var (result, reservation) = await ConsumeAsync(http, factory, clock);
            http.RequestServices = services;
            deadline.RequireCurrent();
            if (reservation is not null)
            {
                BrowserLogoutRemote.Require(reservation, clock);
                http.Response.Cookies.Delete(BrowserSessionCookies.Logout, BrowserSessionCookies.SessionOptions());
            }
            await result.ExecuteAsync(http);
        }
        catch
        {
            http.RequestServices = services;
            if (http.Response.HasStarted || bounded.IsCancellationRequested)
            {
                http.Abort();
                return;
            }
            http.Response.Headers.Clear();
            http.Response.ContentLength = null;
            await BrowserLogoutPage.ReturnUnavailable(http).ExecuteAsync(http);
        }
        finally { http.RequestServices = services; http.RequestAborted = cancellation; }
    }

    /// <summary>Authenticates canonical state and both separate cookies before atomically consuming exactly the persisted original correlation.</summary>
    private static async Task<(IResult Result, BrowserLogoutReservation? Reservation)> ConsumeAsync(HttpContext http,
        IServiceScopeFactory factory, TimeProvider clock)
    {
        await using var scope = factory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        http.RequestServices = services;
        var state = await BrowserLogoutRequest.ReturnAsync(http, services.GetRequiredService<BrowserSessionDestinations>());
        if (state is null || !BrowserLoginRequest.Cookie(http.Request, BrowserSessionCookies.Generation, out var generation) ||
            !BrowserLoginRequest.Cookie(http.Request, BrowserSessionCookies.Logout, out var cookie) ||
            services.GetRequiredService<BrowserLoginProof>().Generation(generation, clock.GetUtcNow()) is not { } browser ||
            services.GetRequiredService<BrowserLogoutProof>().Read(cookie, state, browser, clock.GetUtcNow()) is not { } proof)
        {
            return (Results.BadRequest(), null);
        }
        var operation = new BrowserLogoutService(services.GetRequiredService<AdminSessionDbContext>(), clock, services.GetRequiredService<SessionTicketCodec>());
        var result = await operation.TransitionAsync(proof.BrowserDigest, proof.Digest, proof.Generation, returned: true,
            http.RequestAborted, proof.Created, proof.Expires);
        return result is null ? (Results.Conflict(), null) : (BrowserLogoutPage.Returned(http), result);
    }
}
