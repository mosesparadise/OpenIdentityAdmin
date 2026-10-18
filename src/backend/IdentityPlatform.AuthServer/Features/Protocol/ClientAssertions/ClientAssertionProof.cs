using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;

/// <summary>Binds one cryptographically validated compact assertion to its original native authentication request before durable consumption.</summary>
internal sealed class ClientAssertionProof
{
    private readonly OpenIddictServerEvents.ValidateTokenContext validation;
    private readonly ProtocolStorageRequest request;
    private readonly string compact;
    private readonly string principalBinding;
    private readonly ClaimsPrincipal principal;
    private int claimed;
    internal AdministratorAssertion Assertion
    {
        get;
    }

    /// <summary>Captures native proof and original lifetime without retaining printable proof values in diagnostics.</summary>
    private ClientAssertionProof(OpenIddictServerEvents.ValidateTokenContext validation, AdministratorAssertion assertion, ProtocolStorageRequest request)
    {
        this.validation = validation;
        this.request = request;
        compact = validation.Token;
        principal = validation.Principal!;
        principalBinding = Binding(principal);
        Assertion = assertion;
    }

    /// <summary>Creates proof only from the real native crypto result, exact expected key material and bounded original JSON.</summary>
    internal static ClientAssertionProof? Capture(OpenIddictServerEvents.ValidateTokenContext context, AdministratorRegistrationPolicy policy)
    {
        var original = ProtocolStorageAmbient.Current?.Request;
        var assertion = context.Options.Issuer is { } issuer ? ClientAssertionShape.Read(context, issuer.AbsoluteUri, policy.Key.KeyId) : null;
        return original is not null && assertion is not null && context.TokenValidationResult?.SecurityToken?.SigningKey is { } key &&
            policy.Key.MatchesValidatedKey(key) ? new(context, assertion, original) : null;
    }

    /// <summary>Claims once for the same native transaction and original request; foreign or repeated claims cannot consume or authenticate.</summary>
    internal bool Claim(OpenIddictServerEvents.ProcessAuthenticationContext context) =>
        Interlocked.Exchange(ref claimed, 1) == 0 && IsCurrent(context);

    /// <summary>Rejects any replacement or mutation of the original native proof before or after awaited durable work.</summary>
    internal bool IsCurrent(OpenIddictServerEvents.ProcessAuthenticationContext context) =>
        ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) && ReferenceEquals(validation.Transaction, context.Transaction) &&
        !validation.IsRejected && !validation.IsRequestHandled && !validation.IsRequestSkipped &&
        !context.IsRejected && !context.IsRequestHandled && !context.IsRequestSkipped &&
        ReferenceEquals(principal, validation.Principal) && ReferenceEquals(principal, context.ClientAssertionPrincipal) &&
        principalBinding == Binding(principal) && compact == validation.Token && compact == context.ClientAssertion &&
        compact == context.Request.ClientAssertion && context.ClientId == AdministratorProtocolIdentity.ClientId &&
        context.Request.ClientId == AdministratorProtocolIdentity.ClientId &&
        context.ClientAssertionType == OpenIddictConstants.ClientAssertionTypes.JwtBearer &&
        context.Request.ClientAssertionType == OpenIddictConstants.ClientAssertionTypes.JwtBearer && string.IsNullOrEmpty(context.Request.ClientSecret);

    /// <summary>Detects original native principal mutation without logging or serializing its clear values outside the request.</summary>
    private static string Binding(ClaimsPrincipal principal) => FactorBinding.Digest(principal.Claims.Select(claim => new
    {
        claim.Type,
        claim.Value,
        claim.ValueType,
        claim.Issuer,
        claim.OriginalIssuer,
        Properties = claim.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray()
    }).ToArray());
}
