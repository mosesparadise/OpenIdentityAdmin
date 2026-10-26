using System.Text.Encodings.Web;
using IdentityPlatform.DesignTokens;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Client.AspNetCore;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Owns finite local responses and transfers only a disposed, committed reservation into the native HTTP boundary.</summary>
internal static class BrowserLoginBoundary
{
    internal const string Login = "/bff/login";
    internal const string Unavailable = "Browser login is unavailable.";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string FormPage = "<p>Sign in with your password and verification code.</p><form method=\"post\" action=\"{0}\"><input type=\"hidden\" name=\"{1}\" value=\"{2}\"><button type=\"submit\">Start sign-in</button></form>";
    private const string Title = "Administrator sign-in";
    private const string FormPolicy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self' ";
    private static readonly System.Text.CompositeFormat PageFormat = System.Text.CompositeFormat.Parse(FormPage);
    private const string Html = "text/html; charset=utf-8";
    private const string NoStore = "no-store";
    private const string NoReferrer = "no-referrer";
    private const string FormReferrer = "strict-origin";

    /// <summary>Runs before authentication middleware and keeps one original deadline through body parsing, local disposal and native transport.</summary>
    internal static async Task RunAsync(HttpContext http, IServiceScopeFactory factory, TimeProvider clock)
    {
        var originalServices = http.RequestServices;
        var originalCancellation = http.RequestAborted;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(originalCancellation);
        cancellation.CancelAfter(ProtocolStorageLifetime.RequestLimit);
        BrowserLoginPublication? publication = null;
        var request = new ProtocolStorageRequest(clock, cancellation.Token, () => publication?.Require(clock));
        http.RequestAborted = cancellation.Token;
        SafeHeaders(http);
        try
        {
            if (http.Features.Get<OpenIddictClientAspNetCoreFeature>() is not null || http.Response.HasStarted)
            {
                throw new InvalidOperationException(Unavailable);
            }
            var (result, reservation) = await LocalAsync(http, factory);
            http.RequestServices = originalServices;
            request.RequireCurrent();
            publication = http.Features.Get<BrowserLoginPublication>();
            publication?.Require(clock);
            if (reservation is null)
            {
                if (http.Features.Get<BrowserLogoutRequest>() is { } logout)
                {
                    await BrowserLogoutRemote.BeginAsync(http, factory, clock, request, logout);
                }
                else
                {
                    await result!.ExecuteAsync(http);
                }
            }
            else
            {
                http.Features.Set(reservation);
                await ProtocolHttpBoundary.RunAsync(http, factory, request, ChallengeAsync);
            }
        }
        catch (AntiforgeryValidationException) { http.RequestServices = originalServices; await FailAsync(http, StatusCodes.Status400BadRequest); }
        catch { http.RequestServices = originalServices; await FailAsync(http, StatusCodes.Status503ServiceUnavailable); }
        finally
        {
            http.Features.Set<BrowserLoginReservation>(null);
            http.Features.Set<BrowserLogoutRequest>(null);
            http.Features.Set<BrowserLogoutCookie>(null);
            http.RequestServices = originalServices;
            http.RequestAborted = originalCancellation;
        }
    }

    /// <summary>Disposes every local scope before returning a response or one-use native reservation; no Sessions lock spans HTTP.</summary>
    private static async Task<(IResult? Result, BrowserLoginReservation? Reservation)> LocalAsync(HttpContext http, IServiceScopeFactory factory)
    {
        await using var scope = factory.CreateAsyncScope();
        http.RequestServices = scope.ServiceProvider;
        if (http.Request.Path == BrowserSessionError.Path)
        {
            return (await BrowserSessionError.ResultAsync(http), null);
        }
        if (http.Request.Path == BrowserLogoutPage.Path)
        {
            return (await scope.ServiceProvider.GetRequiredService<BrowserLogoutHandler>().RunAsync(http), null);
        }
        var service = scope.ServiceProvider.GetRequiredService<BrowserLoginService>();
        if (http.Request.Path == BrowserSessionDestinations.ResumePath)
        {
            var reservation = await service.ResumeAsync(http);
            return reservation is null ? (Results.BadRequest(), null) : (null, reservation);
        }
        if (HttpMethods.IsPost(http.Request.Method))
        {
            return (await service.StartAsync(http), null);
        }
        if (!BrowserLoginRequest.Get(http.Request) || http.Request.Query.Count != 0 || !await BrowserLoginRequest.EmptyAsync(http) || await service.ReadAsync(http, create: true) is null)
        {
            return (Results.BadRequest(), null);
        }
        var tokens = scope.ServiceProvider.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        var token = HtmlEncoder.Default.Encode(tokens.RequestToken ?? throw new InvalidOperationException(Unavailable));
        var content = string.Format(System.Globalization.CultureInfo.InvariantCulture, PageFormat, Login, BrowserRequestPolicy.AntiforgeryField, token);
        http.Response.Headers[ReferrerPolicy] = FormReferrer;
        http.Response.Headers.ContentSecurityPolicy = FormPolicy + scope.ServiceProvider.GetRequiredService<BrowserSessionDestinations>().Issuer.GetLeftPart(UriPartial.Authority) + BrowserDocument.AssetPolicy;
        return (Results.Content(BrowserDocument.Render(Title, content), Html), null);
    }

    /// <summary>Resolves the actual named native handler only inside its substituted owned scope, retaining library request and challenge dispatch.</summary>
    private static async ValueTask<ProtocolResponseOutcome> ChallengeAsync(HttpContext http, ProtocolStorageScope scope, CancellationToken token)
    {
        var reservation = http.Features.Get<BrowserLoginReservation>() ?? throw new InvalidOperationException(Unavailable);
        var provider = scope.Services.GetRequiredService<IAuthenticationHandlerProvider>();
        var handler = await provider.GetHandlerAsync(http, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        if (handler is not IAuthenticationRequestHandler request || handler is not IAuthenticationHandler challenge || await request.HandleRequestAsync())
        {
            throw new InvalidOperationException(Unavailable);
        }
        await challenge.ChallengeAsync(new AuthenticationProperties());
        reservation.RequireCompleted(http);
        token.ThrowIfCancellationRequested();
        return ProtocolResponseOutcome.Committed;
    }

    /// <summary>Discards staged redirect/cookies on unavailable or uncertain local completion without claiming rollback.</summary>
    private static Task FailAsync(HttpContext http, int status)
    {
        if (http.Response.HasStarted || http.RequestAborted.IsCancellationRequested)
        {
            http.Abort();
            return Task.CompletedTask;
        }
        http.Response.Headers.Clear();
        SafeHeaders(http);
        http.Response.StatusCode = status;
        return http.Request.Path == BrowserLogoutPage.Path && status == StatusCodes.Status503ServiceUnavailable
            ? BrowserLogoutPage.Unconfirmed(http).ExecuteAsync(http) : Task.CompletedTask;
    }

    /// <summary>Prevents caching and referrer disclosure for all login pages, mutations and protocol returns.</summary>
    private static void SafeHeaders(HttpContext http)
    {
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers[ReferrerPolicy] = NoReferrer;
    }
}
