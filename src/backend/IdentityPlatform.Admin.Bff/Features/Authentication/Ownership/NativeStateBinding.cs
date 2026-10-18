using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Freezes the prepared challenge while permitting only the native token identifier and state output additions.</summary>
internal sealed class NativeStateBinding(OpenIddictClientEvents.ProcessChallengeContext context)
{
    private readonly ClaimsPrincipal principal = context.StateTokenPrincipal!;
    private readonly OpenIddictClientRegistration registration = context.Registration;
    private readonly OpenIddictRequest request = context.Request;
    private readonly OpenIddictClientOptions options = context.Options;
    private readonly string claims = Claims(context.StateTokenPrincipal!, omitIdentifier: true);
    private readonly string parameters = Parameters(context);

    /// <summary>Checks original object identities and all prepared claim/transport values, including after actual child disposal.</summary>
    internal void RequireCurrent()
    {
        if (!ReferenceEquals(principal, context.StateTokenPrincipal) || !ReferenceEquals(registration, context.Registration) ||
            !ReferenceEquals(request, context.Request) || !ReferenceEquals(options, context.Options) ||
            claims != Claims(principal, omitIdentifier: true) || parameters != Parameters(context) ||
            context.IsRejected || context.IsRequestHandled || context.IsRequestSkipped)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Hashes values without retaining a printable credential/correlation snapshot.</summary>
    internal static string Claims(ClaimsPrincipal principal, bool omitIdentifier = false) =>
        ProtocolTokenDescriptorSnapshot.Claims(principal, omitIdentifier);

    /// <summary>Captures the exact prepared request, registration and native state/correlation inputs before generation starts.</summary>
    private static string Parameters(OpenIddictClientEvents.ProcessChallengeContext value) => FactorBinding.Digest(new
    {
        value.IncludeStateToken,
        value.GenerateStateToken,
        value.LoginHint,
        value.IdentityTokenHint,
        value.Registration.DisablePushedAuthorizationRequests,
        Audiences = value.Audiences.Order(StringComparer.Ordinal).ToArray(),
        Resources = value.Resources.Order(StringComparer.Ordinal).ToArray(),
        Custom = value.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new { pair.Key, Value = pair.Value.GetRawValue() }).ToArray(),
        value.ClientId,
        value.RedirectUri,
        value.GrantType,
        value.ResponseType,
        value.ResponseMode,
        value.CodeVerifier,
        value.CodeChallenge,
        value.CodeChallengeMethod,
        value.Nonce,
        value.RequestForgeryProtection,
        value.TargetLinkUri,
        value.BaseUri,
        value.RequestUri,
        value.Registration.RegistrationId,
        value.Registration.Issuer,
        Scopes = value.Scopes.Order(StringComparer.Ordinal).ToArray(),
        Parameters = value.Request.GetParameters().OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new { pair.Key, Value = pair.Value.GetRawValue() }).ToArray()
    });
}
