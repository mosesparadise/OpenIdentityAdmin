using System.Globalization;
using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Projects current admission into the fixed native principal while preserving library-owned private bindings.</summary>
internal static class AdministratorPrincipal
{
    internal const string SessionId = "sid";
    internal const string FactorTime = "oi_admin_factor_time";
    internal const string SourceExpiry = "oi_admin_source_expiry";
    internal const string ProfileRevision = "oi_admin_profile_revision";
    internal const string MaximumAge = "oi_admin_maximum_age";
    private const string PasswordMethod = "pwd";
    private const string FactorMethod = "otp";
    private const int FullAuthenticationMethodCount = 2;
    private const int MultiplicityProbeCount = 2;

    /// <summary>Copies only native private claims, then derives public identity and proof fields from one checked current observation.</summary>
    internal static ClaimsPrincipal Project(ClaimsPrincipal native, ProtocolAdmissionClaims current, long revision, long? maximumAge)
    {
        var principal = native.Clone(claim => claim.Type.StartsWith(OpenIddictConstants.Claims.Prefixes.Private, StringComparison.Ordinal));
        principal.SetClaim(OpenIddictConstants.Claims.Subject, current.UserId);
        principal.SetClaim(SessionId, current.SessionId);
        principal.SetClaim(OpenIddictConstants.Claims.AuthenticationTime, current.PasswordVerifiedAt);
        principal.SetClaims(OpenIddictConstants.Claims.AuthenticationMethodReference, [PasswordMethod, FactorMethod]);
        principal.SetClaim(FactorTime, current.FactorVerifiedAt);
        principal.SetClaim(SourceExpiry, current.SourceExpiresAt);
        principal.SetClaim(ProfileRevision, revision);
        principal.SetClaim(MaximumAge, maximumAge);
        principal.SetScopes(OpenIddictConstants.Scopes.OpenId, AdministratorProtocolIdentity.Scope);
        principal.SetResources(AdministratorProtocolIdentity.Resource);
        principal.SetPresenters(AdministratorProtocolIdentity.ClientId);
        principal.SetDestinations(claim => claim.Type switch
        {
            OpenIddictConstants.Claims.Subject or SessionId =>
                [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.AuthenticationTime or OpenIddictConstants.Claims.AuthenticationMethodReference =>
                [OpenIddictConstants.Destinations.IdentityToken],
            _ => []
        });
        return principal;
    }

    /// <summary>Prevents exchange from renewing the original code's session, authentication times, absolute expiry or registration revision.</summary>
    internal static bool Matches(ClaimsPrincipal code, ProtocolAdmissionClaims current, long revision) =>
        Single(code, OpenIddictConstants.Claims.Subject) == current.UserId &&
        Single(code, SessionId) == current.SessionId &&
        ReadInteger(code, OpenIddictConstants.Claims.AuthenticationTime) == current.PasswordVerifiedAt &&
        ReadInteger(code, FactorTime) == current.FactorVerifiedAt && ReadInteger(code, SourceExpiry) == current.SourceExpiresAt &&
        ReadInteger(code, ProfileRevision) == revision &&
        MethodsMatch(code);

    /// <summary>Reads a single original protected age; an omitted age preserves the fixed ceiling and ambiguity fails closed.</summary>
    internal static bool TryReadMaximumAge(ClaimsPrincipal code, out long? age)
    {
        age = ReadInteger(code, MaximumAge);
        return !code.HasClaim(claim => string.Equals(claim.Type, MaximumAge, StringComparison.OrdinalIgnoreCase)) || age is >= 0;
    }

    /// <summary>Accepts one canonical integer claim only; duplicated, signed, fractional and overflowing values are rejected.</summary>
    private static long? ReadInteger(ClaimsPrincipal principal, string type) =>
        Single(principal, type) is { } value && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) &&
        number.ToString(CultureInfo.InvariantCulture) == value ? number : null;

    /// <summary>Checks raw multiplicity before native convenience accessors can normalize repeated values.</summary>
    private static string? Single(ClaimsPrincipal principal, string type) =>
        principal.Claims.Where(claim => string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase)).Take(MultiplicityProbeCount).ToArray() is [var value] &&
        value.Type == type ? value.Value : null;

    /// <summary>Accepts exactly the two scalar full-authentication method values, without array expansion or deduplication.</summary>
    private static bool MethodsMatch(ClaimsPrincipal principal)
    {
        var methods = principal.Claims.Where(claim => string.Equals(claim.Type, OpenIddictConstants.Claims.AuthenticationMethodReference, StringComparison.OrdinalIgnoreCase)).ToArray();
        return methods.Length == FullAuthenticationMethodCount && methods.All(claim => claim.Type == OpenIddictConstants.Claims.AuthenticationMethodReference &&
            claim.ValueType == ClaimValueTypes.String) && methods.Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal).SetEquals([PasswordMethod, FactorMethod]);
    }
}
