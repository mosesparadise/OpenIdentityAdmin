using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Requires fixed native request and protected-code bindings before projecting the approved credential scope.</summary>
internal static class AdministratorDecisionPolicy
{
    /// <summary>Accepts only the noninteractive fixed code/S256/PAR demand, without promoting a wider or narrower request.</summary>
    internal static bool AllowsAuthorization(OpenIddictRequest request, out long? age)
    {
        age = null;
        return !string.IsNullOrEmpty(request.RequestUri) && AllowsPushedAuthorization(request, out age);
    }

    /// <summary>Maps the approved scope to its fixed server-side resource; caller-supplied resource indicators or audiences are unsupported.</summary>
    internal static bool AllowsPushedAuthorization(OpenIddictRequest request, out long? age)
    {
        age = null;
        return request.ClientId == AdministratorProtocolIdentity.ClientId && request.IsAuthorizationCodeFlow() &&
            request.Prompt == OpenIddictConstants.PromptValues.None &&
            (string.IsNullOrEmpty(request.ResponseMode) || request.ResponseMode == OpenIddictConstants.ResponseModes.Query) &&
            request.CodeChallengeMethod == OpenIddictConstants.CodeChallengeMethods.Sha256 && ProofKeyPolicy.IsChallenge(request.CodeChallenge) &&
            request.GetScopes().ToHashSet(StringComparer.Ordinal).SetEquals([OpenIddictConstants.Scopes.OpenId, AdministratorProtocolIdentity.Scope]) &&
            !request.HasParameter(OpenIddictConstants.Parameters.Resource) && !request.HasParameter(OpenIddictConstants.Parameters.Audience) &&
            AuthenticationFreshness.TryGetMaximumAge(request, out age);
    }

    /// <summary>Rejects ordinary native cached authorization requests and foreign presenters even when the token type matches.</summary>
    internal static bool IsPushedRequest(ClaimsPrincipal principal) =>
        principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken) &&
        principal.GetClaim(OpenIddictConstants.Claims.Private.RequestTokenType) == OpenIddictConstants.RequestTokenTypes.Private.PushedAuthorizationRequest &&
        principal.GetPresenters().SequenceEqual([AdministratorProtocolIdentity.ClientId], StringComparer.Ordinal);

    /// <summary>Requires exact original protected scopes/resource/presenter and nonempty native/source bindings before any projection.</summary>
    internal static bool IsCode(ClaimsPrincipal principal) => principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode) &&
        !string.IsNullOrEmpty(principal.GetTokenId()) && !string.IsNullOrEmpty(principal.GetAuthorizationId()) &&
        !string.IsNullOrEmpty(principal.GetClaim(OpenIddictConstants.Claims.Subject)) && !string.IsNullOrEmpty(principal.GetClaim(AdministratorPrincipal.SessionId)) &&
        HasFixedBindings(principal);

    /// <summary>Requires exact scope/resource/presenter sets before and after projection without widening protected authority.</summary>
    internal static bool HasFixedBindings(ClaimsPrincipal principal) =>
        principal.GetScopes().ToHashSet(StringComparer.Ordinal).SetEquals([OpenIddictConstants.Scopes.OpenId, AdministratorProtocolIdentity.Scope]) &&
        principal.GetResources().SequenceEqual([AdministratorProtocolIdentity.Resource], StringComparer.Ordinal) &&
        principal.GetPresenters().SequenceEqual([AdministratorProtocolIdentity.ClientId], StringComparer.Ordinal);
}
