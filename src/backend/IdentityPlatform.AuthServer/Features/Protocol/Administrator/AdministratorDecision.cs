using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Binds a minimal endpoint decision to actual native authentication and opaque source selectors, never to reusable admission authority.</summary>
internal sealed class AdministratorDecision
{
    private static readonly object Key = new();
    private readonly HttpContext http;
    private readonly ProtocolStorageRequest request;
    private readonly OpenIddictRequest nativeRequest;
    private readonly OpenIddictServerEndpointType endpoint;
    private readonly ClaimsPrincipal original;
    private readonly ClaimsPrincipal signIn;
    private readonly string parameters;
    private int claimed;
    internal string UserId
    {
        get;
    }
    internal string SessionId
    {
        get;
    }
    internal long? MaximumAge
    {
        get;
    }
    internal ClaimsPrincipal Original => original;

    /// <summary>Freezes source selectors and request parameters before the native sign-in pipeline can mutate its separate principal.</summary>
    private AdministratorDecision(HttpContext http, OpenIddictRequest nativeRequest, OpenIddictServerEndpointType endpoint,
        ClaimsPrincipal original, ClaimsPrincipal signIn, string user, string session, long? age)
    {
        this.http = http;
        request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        this.nativeRequest = nativeRequest;
        this.endpoint = endpoint;
        this.original = original;
        this.signIn = signIn;
        UserId = user;
        SessionId = session;
        MaximumAge = age;
        parameters = Parameters(nativeRequest);
    }

    /// <summary>Reads only the named full administrator cookie and actual native PAR authentication; later locked reads decide authority.</summary>
    internal static async Task<IResult> AuthorizeAsync(HttpContext http)
    {
        var native = http.GetOpenIddictServerRequest() ?? throw ProtocolStorageOwner.Unavailable();
        if (!AdministratorDecisionPolicy.AllowsAuthorization(native, out var age))
        {
            return NativeErrorOrigin.Deny(http, OpenIddictConstants.Errors.InvalidRequest);
        }
        var authentication = await http.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var original = authentication.Properties?.GetParameter<ClaimsPrincipal>(OpenIddictServerAspNetCoreConstants.Properties.RequestTokenPrincipal);
        if (!authentication.Succeeded || original is null || !AdministratorDecisionPolicy.IsPushedRequest(original))
        {
            return NativeErrorOrigin.Deny(http, OpenIddictConstants.Errors.InvalidRequest);
        }
        var candidate = await AdministratorSecurityBrowser.CandidateAsync(http);
        if (candidate is null || string.IsNullOrEmpty(candidate.UserId) || string.IsNullOrEmpty(candidate.SessionId))
        {
            return NativeErrorOrigin.Deny(http, OpenIddictConstants.Errors.LoginRequired);
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme));
        principal.SetClaim(OpenIddictConstants.Claims.Subject, candidate.UserId);
        return Attach(http, new(http, native, OpenIddictServerEndpointType.Authorization, original, principal,
            candidate.UserId, candidate.SessionId, age));
    }

    /// <summary>Uses an independent clone of the authenticated code so native restoration and projection cannot mutate the consumed original.</summary>
    internal static async Task<IResult> ExchangeAsync(HttpContext http)
    {
        var native = http.GetOpenIddictServerRequest() ?? throw ProtocolStorageOwner.Unavailable();
        if (native.ClientId != AdministratorProtocolIdentity.ClientId || !native.IsAuthorizationCodeGrantType() || !ProofKeyPolicy.IsVerifier(native.CodeVerifier))
        {
            return NativeErrorOrigin.Deny(http, OpenIddictConstants.Errors.InvalidGrant);
        }
        var authentication = await http.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var original = authentication.Principal;
        if (!authentication.Succeeded || original is null || !AdministratorDecisionPolicy.IsCode(original) ||
            !AdministratorPrincipal.TryReadMaximumAge(original, out var age))
        {
            return NativeErrorOrigin.Deny(http, OpenIddictConstants.Errors.InvalidGrant);
        }
        var principal = original.Clone(static _ => true);
        return Attach(http, new(http, native, OpenIddictServerEndpointType.Token, original, principal,
            original.GetClaim(OpenIddictConstants.Claims.Subject)!, original.GetClaim(AdministratorPrincipal.SessionId)!, age));
    }

    /// <summary>Attaches a single decision without overwriting a previous attempt or granting a publication permit.</summary>
    private static IResult Attach(HttpContext http, AdministratorDecision decision)
    {
        http.Items.Add(Key, decision);
        return Results.SignIn(decision.signIn, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Claims the decision once only from its exact native sign-in and original request.</summary>
    internal static AdministratorDecision Claim(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var http = context.Transaction.GetHttpRequest()?.HttpContext ?? throw ProtocolStorageOwner.Unavailable();
        if (!http.Items.Remove(Key, out var value) || value is not AdministratorDecision decision ||
            Interlocked.Exchange(ref decision.claimed, 1) != 0 || !ReferenceEquals(decision.signIn, context.Principal) || !decision.IsCurrent(context))
        {
            ProtocolStorageAmbient.Current?.Request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        return decision;
    }

    /// <summary>Checks exact native inputs after awaited work without accepting parameter mutation on the same request object.</summary>
    internal bool IsCurrent(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var authentication = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        return ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) && ReferenceEquals(http, context.Transaction.GetHttpRequest()?.HttpContext) &&
            ReferenceEquals(nativeRequest, context.Request) && endpoint == context.EndpointType && !context.IsRejected && !context.IsRequestSkipped &&
            parameters == Parameters(context.Request) && ReferenceEquals(original, endpoint == OpenIddictServerEndpointType.Authorization
                ? authentication?.RequestTokenPrincipal : authentication?.AuthorizationCodePrincipal);
    }

    /// <summary>Detects request changes without retaining printable request or credential material in diagnostics.</summary>
    private static string Parameters(OpenIddictRequest request) => FactorBinding.Digest(request.GetParameters().OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new { pair.Key, Value = pair.Value.ToString() }).ToArray());
}
