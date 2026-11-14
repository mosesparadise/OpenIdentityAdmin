using System.Globalization;
using System.Security.Claims;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.UserInfo;

/// <summary>Retains the earlier signed expiry even after native validation restores private lifetime metadata from storage.</summary>
internal static class UserInfoLifetime
{
    private const int AmbiguousClaimCount = 2;
    /// <summary>Bounds the validated signed access token and current native entry independently; equality, missing or malformed signed expiry denies.</summary>
    internal static DateTimeOffset? Bound(ClaimsPrincipal principal, DateTimeOffset native, DateTimeOffset now)
    {
        var claims = principal.FindAll(OpenIddictConstants.Claims.ExpiresAt).Take(AmbiguousClaimCount).ToArray();
        if (claims is not [var claim] || claim.Type != OpenIddictConstants.Claims.ExpiresAt ||
            claim.ValueType is not (ClaimValueTypes.Integer32 or ClaimValueTypes.Integer64) ||
            !long.TryParse(claim.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds() || claim.Value != seconds.ToString(CultureInfo.InvariantCulture))
        {
            return null;
        }
        var signed = DateTimeOffset.FromUnixTimeSeconds(seconds);
        var bound = signed < native ? signed : native;
        return bound > now ? bound : null;
    }
}
