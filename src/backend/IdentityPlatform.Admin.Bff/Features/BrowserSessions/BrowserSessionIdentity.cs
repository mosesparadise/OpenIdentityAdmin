using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Core.Features.Identity.Authentication;
using Microsoft.AspNetCore.Authentication;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Projects a previously native-validated B3 identity into a minimal local ticket; grants no callback or persistence authority.</summary>
internal sealed class BrowserSessionIdentity
{
    private const int MultiplicityProbe = 2;
    private const int MethodMultiplicityProbe = BrowserSessionClaims.MethodCount + 1;
    private readonly string subject;
    private readonly string session;

    /// <summary>Captures immutable validated bindings without retaining the raw token, principal or extra claims.</summary>
    private BrowserSessionIdentity(string subject, string session, DateTimeOffset sourceExpiry)
    {
        this.subject = subject;
        this.session = session;
        SourceExpiresAt = sourceExpiry;
    }

    internal DateTimeOffset SourceExpiresAt
    {
        get;
    }

    /// <summary>Requires the exact native-validated token/principal and protected expected intent; callers still own consumption, generation and atomic commit.</summary>
    internal static BrowserSessionIdentity? Validate(ClaimsPrincipal principal, string validatedIdentityToken,
        string expectedIntent, DateTimeOffset now)
    {
        var identities = principal.Identities.Take(MultiplicityProbe).ToArray();
        if (identities is not [{ IsAuthenticated: true, Actor: null }] || !BrowserSessionClaims.CanonicalIntent(expectedIntent) ||
            BrowserSessionClaims.Read(validatedIdentityToken) is not { } claims)
        {
            return null;
        }
        var subject = BrowserSessionClaims.Binding(claims[BrowserSessionClaims.Subject]);
        var session = BrowserSessionClaims.Binding(claims[BrowserSessionClaims.Session]);
        var intent = BrowserSessionClaims.Binding(claims[BrowserSessionClaims.Intent]);
        var expiry = BrowserSessionClaims.Integer(claims[BrowserSessionClaims.SourceExpiry]);
        var authenticated = BrowserSessionClaims.Integer(claims[BrowserSessionClaims.AuthenticationTime]);
        if (subject is null || session is null || intent != expectedIntent ||
            expiry is not { } end || authenticated is not { } started || !Current(end, started, now) ||
            !BrowserSessionClaims.FullMethods(claims[BrowserSessionClaims.Methods]) || !Matches(principal, claims))
        {
            return null;
        }
        return new(subject, session, DateTimeOffset.FromUnixTimeSeconds(end));
    }

    /// <summary>Builds a fresh canonical identity with only subject/session; no native roles, tokens, properties, actor or refresh authority is copied.</summary>
    internal AuthenticationTicket CreateTicket() => new(new ClaimsPrincipal(new ClaimsIdentity(
        [new(BrowserSessionClaims.Subject, subject), new(BrowserSessionClaims.Session, session)], SessionTicketPolicy.Scheme)),
        new AuthenticationProperties { AllowRefresh = false, IsPersistent = false }, SessionTicketPolicy.Scheme);

    /// <summary>Validates the supplied original deadline without reconstructing it from password time or the shorter ID-token expiry.</summary>
    private static bool Current(long expiry, long authenticated, DateTimeOffset now)
    {
        var end = DateTimeOffset.FromUnixTimeSeconds(expiry);
        var started = DateTimeOffset.FromUnixTimeSeconds(authenticated);
        return end > now && end > started && end - now <= TimeSpan.FromSeconds(AdministratorPolicy.SessionSeconds) &&
            started - now <= TimeSpan.FromSeconds(AdministratorPolicy.ClockSkewSeconds) &&
            now - started < TimeSpan.FromSeconds(AdministratorPolicy.FreshnessSeconds);
    }

    /// <summary>Cross-checks raw shapes against the native principal, rejecting multiplicity, aliases, substitution and method normalization.</summary>
    private static bool Matches(ClaimsPrincipal principal, Dictionary<string, JsonElement> payload)
    {
        foreach (var (name, value) in payload)
        {
            var claims = principal.Claims.Where(claim => string.Equals(claim.Type, name, StringComparison.OrdinalIgnoreCase)).Take(MethodMultiplicityProbe).ToArray();
            if (name == BrowserSessionClaims.Methods)
            {
                if (claims.Length != BrowserSessionClaims.MethodCount || claims.Any(claim => claim.Type != name || claim.ValueType != ClaimValueTypes.String) ||
                    !claims.Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal).SetEquals([BrowserSessionClaims.PasswordMethod, BrowserSessionClaims.FactorMethod]))
                {
                    return false;
                }
            }
            else if (claims is not [var claim] || claim.Type != name || !MatchesValue(claim, value))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Checks native value types as well as values; textual dates cannot masquerade as authenticated numeric fields.</summary>
    private static bool MatchesValue(Claim claim, JsonElement value) => value.ValueKind == JsonValueKind.String ?
        claim.ValueType == ClaimValueTypes.String && claim.Value == value.GetString() :
        claim.ValueType is ClaimValueTypes.Integer or ClaimValueTypes.Integer32 or ClaimValueTypes.Integer64 or
            ClaimValueTypes.UInteger32 or ClaimValueTypes.UInteger64 &&
        claim.Value == BrowserSessionClaims.Integer(value)?.ToString(CultureInfo.InvariantCulture);
}
