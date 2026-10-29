using System.Globalization;
using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Admin.Api.Features.Authentication;

/// <summary>Accepts only canonical bindings from an already native-validated administrator access token.</summary>
internal sealed record ApiIdentity(string Subject, string Session, string TokenId, string AuthorizationId, DateTimeOffset ExpiresAt)
{
    private const string SignedExpiration = "exp";
    private const string SessionClaim = "sid";
    private const int BindingLimit = 400;
    private const int MultiplicityProbe = 2;

    /// <summary>Requires exact resource/scope/presenter and native persisted identifiers; roles never supply current authority.</summary>
    internal static ApiIdentity? Read(ClaimsPrincipal principal, DateTimeOffset now)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !principal.HasScope(AdministratorProtocolIdentity.Scope) ||
            !principal.GetAudiences().SequenceEqual([AdministratorProtocolIdentity.Resource], StringComparer.Ordinal) ||
            !principal.GetPresenters().SequenceEqual([AdministratorProtocolIdentity.ClientId], StringComparer.Ordinal) ||
            Single(principal, OpenIddictConstants.Claims.ClientId) != AdministratorProtocolIdentity.ClientId ||
            Single(principal, OpenIddictConstants.Claims.Subject) is not { } subject ||
            Single(principal, SessionClaim) is not { } session ||
            principal.GetTokenId() is not { Length: > 0 and <= BindingLimit } token ||
            principal.GetAuthorizationId() is not { Length: > 0 and <= BindingLimit } authorization ||
            principal.GetExpirationDate() is not { } expiry || expiry <= now ||
            SignedExpiry(principal) is not { } signedExpiry || signedExpiry <= now)
        {
            return null;
        }
        return new(subject, session, token, authorization, signedExpiry < expiry ? signedExpiry : expiry);
    }

    /// <summary>Retains the cryptographically validated public NumericDate even when native token-entry restoration supplies a different persisted deadline.</summary>
    private static DateTimeOffset? SignedExpiry(ClaimsPrincipal principal)
    {
        var claims = principal.Claims.Where(claim => string.Equals(claim.Type, SignedExpiration, StringComparison.OrdinalIgnoreCase))
            .Take(MultiplicityProbe).ToArray();
        if (claims is not [var claim] || claim.Type != SignedExpiration ||
            !long.TryParse(claim.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds.ToString(CultureInfo.InvariantCulture) != claim.Value)
        {
            return null;
        }
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>Rejects aliases, duplicates, coercion and oversized identifiers before any database lookup.</summary>
    private static string? Single(ClaimsPrincipal principal, string name) =>
        principal.Claims.Where(claim => string.Equals(claim.Type, name, StringComparison.OrdinalIgnoreCase)).Take(MultiplicityProbe).ToArray()
            is [var claim] && claim.Type == name && claim.ValueType == ClaimValueTypes.String && claim.Value is { Length: > 0 and <= BindingLimit }
                ? claim.Value : null;
}
