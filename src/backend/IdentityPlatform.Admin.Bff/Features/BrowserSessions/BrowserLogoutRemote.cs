using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Owns optional remote preparation after known local success, keeping failure and cleanup independent from local status.</summary>
internal static class BrowserLogoutRemote
{
    private const string Endpoint = "/connect/end-session";
    private const string Location = "Location";
    private const string CookieAssignment = "=";
    private const string Hint = "logout_hint";

    /// <summary>Retains local cookies and truthful status even when the independent remote operation cannot safely publish navigation.</summary>
    internal static async Task BeginAsync(HttpContext http, IServiceScopeFactory factory, TimeProvider clock,
        ProtocolStorageRequest deadline, BrowserLogoutRequest request)
    {
        var services = http.RequestServices;
        var localCookies = new Microsoft.Extensions.Primitives.StringValues(http.Response.Headers.SetCookie
            .Where(value => value is not null && !value.StartsWith(BrowserSessionCookies.Generation + CookieAssignment, StringComparison.Ordinal)).ToArray());
        http.Response.Headers.SetCookie = localCookies;
        try
        {
            var prepared = await PrepareAsync(http, factory, clock, request);
            http.RequestServices = services;
            deadline.RequireCurrent();
            if (prepared is null)
            {
                PublishLocal(http, clock);
                await BrowserLogoutPage.Incomplete(http).ExecuteAsync(http);
                return;
            }
            var (link, cookie, reservation) = prepared.Value;
            Require(reservation, clock);
            http.Features.Get<BrowserLogoutCookie>()?.Publish(http, clock);
            var options = BrowserSessionCookies.Bounded(clock.GetUtcNow(), new DateTimeOffset(reservation.ExpiresAt, TimeSpan.Zero))
                ?? throw new InvalidOperationException(BrowserLoginBoundary.Unavailable);
            http.Response.Cookies.Append(BrowserSessionCookies.Logout, cookie, options);
            await BrowserLogoutPage.Continue(http, link).ExecuteAsync(http);
        }
        catch
        {
            http.RequestServices = services;
            if (http.Response.HasStarted || http.RequestAborted.IsCancellationRequested)
            {
                http.Abort();
                return;
            }
            try
            {
                deadline.RequireCurrent();
            }
            catch (OperationCanceledException) { http.Abort(); return; }
            http.Response.Headers.SetCookie = localCookies;
            http.Response.Headers.Remove(Location);
            http.Response.ContentLength = null;
            PublishLocal(http, clock);
            await BrowserLogoutPage.Incomplete(http).ExecuteAsync(http);
        }
        finally { http.RequestServices = services; }
    }

    /// <summary>Known local success survives elapsed proof bounds, but a fresh generation cookie must never escape those original bounds.</summary>
    private static void PublishLocal(HttpContext http, TimeProvider clock)
    {
        try
        {
            http.Features.Get<BrowserLogoutCookie>()?.Publish(http, clock);
        }
        catch (InvalidOperationException) { }
    }

    /// <summary>Consumes only the detached local reservation under new owned persistence; disposal completes before the link or cookie can escape.</summary>
    private static async Task<(string Link, string Cookie, BrowserLogoutReservation Reservation)?> PrepareAsync(HttpContext http,
        IServiceScopeFactory factory, TimeProvider clock, BrowserLogoutRequest request)
    {
        await using var scope = factory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        http.RequestServices = services;
        var original = request.Reservation;
        Require(original, clock);
        var operation = new BrowserLogoutService(services.GetRequiredService<AdminSessionDbContext>(), clock, services.GetRequiredService<SessionTicketCodec>());
        var result = await operation.TransitionAsync(original.BrowserDigest, original.Digest, original.Generation, returned: false,
            http.RequestAborted, original.CreatedAt, original.ExpiresAt);
        if (result is null || result.SourceSession != original.SourceSession)
        {
            return null;
        }
        var proof = new BrowserPendingProof(request.State, result.BrowserDigest, result.Generation, result.CreatedAt, result.ExpiresAt);
        var cookie = services.GetRequiredService<BrowserLogoutProof>().Protect(proof);
        var destinations = services.GetRequiredService<BrowserSessionDestinations>();
        var link = QueryHelpers.AddQueryString(new Uri(destinations.Issuer, Endpoint).AbsoluteUri, new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ClientId] = IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator.AdministratorProtocolIdentity.ClientId,
            [OpenIddictConstants.Parameters.PostLogoutRedirectUri] = destinations.LogoutReturn.AbsoluteUri,
            [Hint] = result.SourceSession,
            [OpenIddictConstants.Parameters.State] = request.State
        });
        return (link, cookie, result);
    }

    /// <summary>Retains the original creation and latest committed observation through all awaited cleanup without opening a new lifetime.</summary>
    internal static void Require(BrowserLogoutReservation reservation, TimeProvider clock)
    {
        if (!BrowserSessionPolicy.Current(reservation.CreatedAt, reservation.ObservedAt, reservation.ExpiresAt, clock.GetUtcNow().UtcTicks))
        {
            throw new InvalidOperationException(BrowserLoginBoundary.Unavailable);
        }
    }
}
