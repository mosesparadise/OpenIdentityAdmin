using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Constrains actual native token clones before generation rather than trusting a snapshot of already altered claims.</summary>
internal static class ManagedPreparedPrincipal
{
    private const int HalfSha256Bytes = 16;
    private static readonly HashSet<string> Common = new(StringComparer.Ordinal)
    {
        Claims.Subject, Claims.Private.AuthorizationId, Claims.Private.TokenId,
        Claims.Private.CreationDate, Claims.Private.ExpirationDate, Claims.Private.Issuer
    };

    /// <summary>Requires exact original subject, native authorization, issuer and bounded lifetime on every prepared credential.</summary>
    internal static bool Matches(OpenIddictServerEvents.ProcessSignInContext context, ClaimsPrincipal principal, string type,
        ManagedLoginInteraction row, string selector, long maximum)
    {
        if (principal.Identities.Count() != 1 || principal.Identity is not ClaimsIdentity { IsAuthenticated: true, Actor: null } ||
            principal.Claims.GroupBy(claim => claim.Type, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() != 1) ||
            principal.GetClaim(Claims.Subject) != row.UserId || principal.GetAuthorizationId() != context.Principal!.GetAuthorizationId() ||
            principal.GetClaim(Claims.Private.Issuer) != (context.Options.Issuer ?? context.BaseUri)!.AbsoluteUri ||
            principal.GetCreationDate() is not { } created || principal.GetExpirationDate() is not { } expires || created >= expires ||
            expires.ToUnixTimeSeconds() > maximum || expires <= context.Options.TimeProvider.GetUtcNow() || !principal.GetResources().IsEmpty)
        {
            return false;
        }
        return type switch
        {
            TokenTypeIdentifiers.Private.AuthorizationCode => Code(context, principal, row, selector),
            TokenTypeIdentifiers.AccessToken => Access(context, principal),
            TokenTypeIdentifiers.IdentityToken => Identity(context, principal),
            _ => false
        };
    }

    /// <summary>Requires the exact protected callback, nonce and S256 binding of the authenticated authorization request.</summary>
    private static bool Code(OpenIddictServerEvents.ProcessSignInContext context, ClaimsPrincipal principal, ManagedLoginInteraction row, string selector) =>
        ManagedPrincipal.Matches(principal, row, context.ClientId!, selector) && principal.GetAudiences().IsEmpty &&
        principal.GetClaim(Claims.Private.RedirectUri) == context.Request.RedirectUri &&
        principal.GetClaim(Claims.Private.CodeChallenge) == context.Request.CodeChallenge &&
        principal.GetClaim(Claims.Private.CodeChallengeMethod) == CodeChallengeMethods.Sha256 &&
        principal.GetClaim(Claims.Private.Nonce) == context.Request.Nonce;

    /// <summary>Admits only OpenID scope with the original presenter, no resource audience and no administrator/private source claims.</summary>
    private static bool Access(OpenIddictServerEvents.ProcessSignInContext context, ClaimsPrincipal principal) =>
        principal.Claims.All(claim => Common.Contains(claim.Type) || claim.Type is Claims.ClientId or Claims.Private.Presenter or Claims.Private.Scope) &&
        principal.Claims.All(claim => claim.GetDestinations().IsEmpty) && principal.GetAudiences().IsEmpty &&
        principal.GetScopes().SequenceEqual([Scopes.OpenId], StringComparer.Ordinal) &&
        principal.GetPresenters().SequenceEqual([context.ClientId!], StringComparer.Ordinal) && principal.GetClaim(Claims.ClientId) == context.ClientId;

    /// <summary>Binds the sole ID-token recipient, original nonce and native access-token hash without admitting extra profile claims.</summary>
    private static bool Identity(OpenIddictServerEvents.ProcessSignInContext context, ClaimsPrincipal principal) =>
        principal.Claims.All(claim => Common.Contains(claim.Type) || claim.Type is Claims.Private.Audience or Claims.AuthorizedParty or Claims.Nonce or Claims.AccessTokenHash) &&
        principal.Claims.All(claim => claim.GetDestinations().IsEmpty) && principal.GetScopes().IsEmpty && principal.GetPresenters().IsEmpty &&
        principal.GetAudiences().SequenceEqual([context.ClientId!], StringComparer.Ordinal) && principal.GetClaim(Claims.AuthorizedParty) == context.ClientId &&
        principal.GetClaim(Claims.Nonce) == ManagedConsumption.Original(context)!.GetClaim(Claims.Private.Nonce) && !string.IsNullOrEmpty(context.AccessToken) &&
        principal.GetClaim(Claims.AccessTokenHash) == Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(context.AccessToken)).AsSpan(0, HalfSha256Bytes).ToArray());
}
