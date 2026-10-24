using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Client.AspNetCore;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Owns the native product callback through one exchange, atomic local establishment and final cookie publication.</summary>
internal static class BrowserCallbackBoundary
{
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string NoReferrer = "no-referrer";
    private const string NoStore = "no-store";

    /// <summary>Keeps the original request budget and immutable receipt alive through all native/local scope cleanup and output.</summary>
    internal static async Task RunAsync(HttpContext http, IServiceScopeFactory factory, TimeProvider clock)
    {
        var original = http.RequestAborted;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(original);
        cancellation.CancelAfter(ProtocolStorageLifetime.RequestLimit);
        var receipt = new BrowserCallbackReceipt(http, factory, clock);
        BrowserSessionTicket? ticket = null;
        var request = new ProtocolStorageRequest(clock, cancellation.Token, () =>
        {
            receipt.Require();
            (ticket ?? throw ProtocolStorageOwner.Unavailable()).Require(clock);
        });
        http.RequestAborted = cancellation.Token;
        http.Features.Set(receipt);
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers[ReferrerPolicy] = NoReferrer;
        try
        {
            await ProtocolHttpBoundary.RunAsync(http, factory, request, async (context, scope, token) =>
            {
                if (!BrowserCallbackRequest.Valid(context.Request) || !await BrowserLoginRequest.EmptyAsync(context) ||
                    context.Features.Get<OpenIddictClientAspNetCoreFeature>() is not null)
                {
                    throw ProtocolStorageOwner.Unavailable();
                }
                await AuthenticateAsync(context, scope);
                receipt.Require();
                scope.Owner.RequireCompleted();
                ticket = await BrowserSessionTicket.EstablishAsync(receipt, factory, clock, token);
                receipt.Require();
                ticket.Require(clock);
                Publish(context, ticket, clock);
                return ProtocolResponseOutcome.Committed;
            });
        }
        finally
        {
            http.Features.Set<BrowserCallbackReceipt>(null);
            http.RequestAborted = original;
        }
    }

    /// <summary>Requires actual named native passthrough and successful authentication; no principal or callback parsing is substituted.</summary>
    private static async Task AuthenticateAsync(HttpContext http, ProtocolStorageScope scope)
    {
        var provider = scope.Services.GetRequiredService<IAuthenticationHandlerProvider>();
        var handler = await provider.GetHandlerAsync(http, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        if (handler is not IAuthenticationRequestHandler request || await request.HandleRequestAsync() ||
            !(await handler.AuthenticateAsync()).Succeeded)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Stages opaque committed material only; the existing native capture still guards disposal, deadlines and actual sending.</summary>
    private static void Publish(HttpContext http, BrowserSessionTicket ticket, TimeProvider clock)
    {
        var options = BrowserSessionCookies.Bounded(clock.GetUtcNow(), new DateTimeOffset(ticket.Expires, TimeSpan.Zero)) ??
            throw ProtocolStorageOwner.Unavailable();
        http.Response.Cookies.Append(BrowserSessionCookies.Session, ticket.Handle, BrowserSessionCookies.SessionOptions());
        http.Response.Cookies.Append(BrowserSessionCookies.Generation, ticket.Generation, options);
        http.Response.Cookies.Delete(BrowserSessionCookies.Prelogin, BrowserSessionCookies.SessionOptions());
        http.Response.Headers.Location = BrowserLoginBoundary.Login;
        http.Response.StatusCode = StatusCodes.Status303SeeOther;
    }
}

/// <summary>Bounds callback framing while leaving state, correlation, issuer and authorization response validation to OpenIddict.</summary>
internal static class BrowserCallbackRequest
{
    private const int QueryLength = 16384;
    private static readonly string[] Names = ["code", "state", "iss", "error", "error_description", "error_uri"];

    /// <summary>Accepts only a bodyless HTTPS query callback with unique finite exact native parameter names.</summary>
    internal static bool Valid(HttpRequest request) => BrowserLoginRequest.Get(request) && request.ContentType is null &&
        request.QueryString.Value is { Length: > 0 and <= QueryLength } && request.Query.Count <= Names.Length &&
        request.Query.All(pair => Names.Contains(pair.Key, StringComparer.Ordinal) && pair.Value is { Count: 1 } && !string.IsNullOrEmpty(pair.Value[0]));
}
