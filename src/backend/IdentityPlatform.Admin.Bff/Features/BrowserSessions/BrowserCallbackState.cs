using System.Globalization;
using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Checks the exact private reserved tuple only after native protected-state validation and consumption.</summary>
internal static class BrowserCallbackState
{
    /// <summary>Requires canonical exact scalar provenance with no token destinations or caller-selected return target.</summary>
    internal static bool Matches(ClaimsPrincipal state, string browser, string intent, long generation, string reservation) =>
        BrowserSessionPolicy.IsDigest(browser) && BrowserSessionPolicy.IsDigest(intent) && BrowserSessionPolicy.IsDigest(reservation) &&
        generation > 0 && !state.Claims.Any(claim => string.Equals(claim.Type, OpenIddictConstants.Claims.TargetLinkUri, StringComparison.OrdinalIgnoreCase)) &&
        Match(state, BrowserSessionIntent.ClaimType, intent, ClaimValueTypes.String) &&
        Match(state, BrowserLoginReservation.BrowserClaim, browser, ClaimValueTypes.String) &&
        Match(state, BrowserLoginReservation.ReservationClaim, reservation, ClaimValueTypes.String) &&
        Match(state, BrowserLoginReservation.GenerationClaim, generation.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64);

    /// <summary>Denies aliases, duplicated fields and coerced values without taking protocol cryptography ownership.</summary>
    private static bool Match(ClaimsPrincipal state, string name, string value, string type)
    {
        var claims = state.Claims.Where(claim => string.Equals(claim.Type, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return claims is [var claim] && claim.Type == name && claim.Value == value && claim.ValueType == type && claim.GetDestinations().IsEmpty;
    }
}
