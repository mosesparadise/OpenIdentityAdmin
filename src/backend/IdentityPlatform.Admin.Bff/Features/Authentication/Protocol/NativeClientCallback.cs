using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Preserves the exact validated and durably consumed state through native callback processing and token exchange.</summary>
internal sealed class NativeClientCallback
{
    private static readonly string Key = Guid.NewGuid().ToString();
    private readonly OpenIddictClientEvents.ProcessAuthenticationContext context;
    private readonly NativeClientProfile profile;
    private readonly ClaimsPrincipal principal;
    private readonly OpenIddictRequest request;
    private readonly string claims;
    private readonly string input;

    /// <summary>Captures state only after the durable redemption handler has confirmed its owning child transaction and cleanup.</summary>
    private NativeClientCallback(OpenIddictClientEvents.ProcessAuthenticationContext context, NativeClientProfile profile)
    {
        this.context = context;
        this.profile = profile;
        principal = context.StateTokenPrincipal ?? throw ProtocolStorageOwner.Unavailable();
        request = context.Request;
        claims = NativeStateBinding.Claims(principal);
        input = Input(request);
        Require(context);
    }

    /// <summary>Publishes only the original operation's private consumed-state receipt, never a signed or caller-constructed authority token.</summary>
    internal static void Attach(OpenIddictClientEvents.ProcessAuthenticationContext context, NativeClientProfile profile)
    {
        if (context.Transaction.GetProperty<NativeClientCallback>(Key) is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        context.Transaction.SetProperty(Key, new NativeClientCallback(context, profile));
    }

    /// <summary>Requires actual early durable redemption before a native token send or callback output may be admitted.</summary>
    internal static NativeClientCallback Find(OpenIddictClientEvents.ProcessAuthenticationContext context) =>
        context.Transaction.GetProperty<NativeClientCallback>(Key) ?? throw ProtocolStorageOwner.Unavailable();

    /// <summary>Checks original callback objects, all protected claims, fixed profile and strict state lifetime after each remote boundary.</summary>
    internal void Require(OpenIddictClientEvents.ProcessAuthenticationContext current)
    {
        if (!ReferenceEquals(current, context) || !ReferenceEquals(current.StateTokenPrincipal, principal) || !ReferenceEquals(current.Request, request) ||
            input != Input(current.Request) || claims != NativeStateBinding.Claims(principal) ||
            !DurableClientState.IsCurrent(principal, current.Options.TimeProvider!) ||
            principal.GetClaim(OpenIddictConstants.Claims.Issuer) != profile.ClientUri.AbsoluteUri ||
            principal.GetClaim(OpenIddictConstants.Claims.AuthorizationServer) != profile.Issuer.AbsoluteUri ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.RedirectUri) != profile.Callback.AbsoluteUri ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.GrantType) != OpenIddictConstants.GrantTypes.AuthorizationCode ||
            principal.GetClaim(OpenIddictConstants.Claims.Private.ResponseType) != OpenIddictConstants.ResponseTypes.Code ||
            !principal.GetScopes().ToHashSet(StringComparer.Ordinal).SetEquals([OpenIddictConstants.Scopes.OpenId, AdministratorProtocolIdentity.Scope]))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Checks the exact native code exchange with no additional grant, resource, secret or caller-selected endpoint.</summary>
    internal void RequireToken(OpenIddictClientEvents.ProcessAuthenticationContext current)
    {
        Require(current);
        IdentityPlatform.Admin.Bff.Features.BrowserSessions.BrowserCallbackReceipt.DemandTransport(current);
        NativeClientMetadata.Require(current.Configuration, profile);
        (NativeClientAssertion.Find(current.Transaction) ?? throw ProtocolStorageOwner.Unavailable())
            .RequireOutput(current.ClientAssertion, current.ClientAssertionPrincipal);
        var token = current.TokenRequest;
        if (!current.SendTokenRequest || !current.GenerateClientAssertion || !current.IncludeClientAssertion ||
            current.GrantType != OpenIddictConstants.GrantTypes.AuthorizationCode ||
            current.TokenEndpointClientAuthenticationMethod != OpenIddictConstants.ClientAuthenticationMethods.PrivateKeyJwt ||
            current.TokenEndpoint?.OriginalString != profile.TokenEndpoint.AbsoluteUri || token is null || token.Count != TokenFieldCount ||
            token.GrantType != OpenIddictConstants.GrantTypes.AuthorizationCode || token.ClientId != AdministratorProtocolIdentity.ClientId ||
            string.IsNullOrEmpty(current.AuthorizationCode) || token.Code != current.AuthorizationCode || token.Code != request.Code ||
            string.IsNullOrEmpty(token.CodeVerifier) || token.CodeVerifier != principal.GetClaim(OpenIddictConstants.Claims.Private.CodeVerifier) ||
            token.RedirectUri != profile.Callback.AbsoluteUri || token.ClientAssertion != current.ClientAssertion ||
            token.ClientAssertionType != OpenIddictConstants.ClientAssertionTypes.JwtBearer)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    private const int TokenFieldCount = 7;

    /// <summary>Retains only an ordinal digest of original callback parameters, without logging code/state or raw query content.</summary>
    private static string Input(OpenIddictRequest request) => FactorBinding.Digest(request.GetParameters().OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new { pair.Key, Value = pair.Value.GetRawValue() }).ToArray());
}
