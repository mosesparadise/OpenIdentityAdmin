using System.Security.Claims;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Owns the private expected-intent claim carried by native protected client state, never a browser-supplied identity assertion.</summary>
internal static class BrowserSessionIntent
{
    internal const string ClaimType = "oi_b3_login_intent";
    private const int MultiplicityProbe = 2;

    /// <summary>Reads only a unique canonical expectation from the exact state principal already validated and consumed by the native callback owner.</summary>
    internal static string? Read(ClaimsPrincipal state)
    {
        var claims = state.Claims.Where(claim => string.Equals(claim.Type, ClaimType, StringComparison.OrdinalIgnoreCase))
            .Take(MultiplicityProbe).ToArray();
        return claims is [var claim] && claim.Type == ClaimType && claim.ValueType == ClaimValueTypes.String &&
            BrowserSessionClaims.CanonicalIntent(claim.Value) ? claim.Value : null;
    }
}
