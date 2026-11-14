using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Owns the finite current-email projection and its ID-token-only destinations, separate from authenticated code provenance.</summary>
internal static class ManagedEmailClaims
{
    private const int PairCount = 2;

    private const string True = "true";
    private const string False = "false";

    /// <summary>Adds an immutable current projection only after source authority; existing or injected email claims are never adopted.</summary>
    internal static void Attach(ClaimsPrincipal principal, ManagedEmailProjection? email)
    {
        if (!Matches(principal, null, destinations: false))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (email is null)
        {
            return;
        }
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Email, email.Address, ClaimValueTypes.String)
            .SetDestinations(OpenIddictConstants.Destinations.IdentityToken));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.EmailVerified, email.Verified ? True : False, ClaimValueTypes.Boolean)
            .SetDestinations(OpenIddictConstants.Destinations.IdentityToken));
    }

    /// <summary>Accepts only the exact captured values and native JSON types; unrelated public claims remain forbidden.</summary>
    internal static bool Allows(Claim claim, ManagedEmailProjection? email) => email is not null &&
        (claim.Type == OpenIddictConstants.Claims.Email && claim.Value == email.Address && claim.ValueType == ClaimValueTypes.String ||
        claim.Type == OpenIddictConstants.Claims.EmailVerified && claim.Value == (email.Verified ? True : False) && claim.ValueType == ClaimValueTypes.Boolean);

    /// <summary>Requires the whole pair or neither field, with exact destinations before native preparation and none on prepared clones.</summary>
    internal static bool Matches(ClaimsPrincipal principal, ManagedEmailProjection? email, bool destinations)
    {
        var claims = principal.Claims.Where(claim => claim.Type is OpenIddictConstants.Claims.Email or OpenIddictConstants.Claims.EmailVerified).ToArray();
        return email is null ? claims.Length == 0 : claims.Length == PairCount &&
            claims.Select(claim => claim.Type).Distinct(StringComparer.Ordinal).Count() == PairCount && claims.All(claim => Allows(claim, email) &&
                (destinations ? claim.GetDestinations().SequenceEqual([OpenIddictConstants.Destinations.IdentityToken], StringComparer.Ordinal) : claim.GetDestinations().IsEmpty));
    }
}
