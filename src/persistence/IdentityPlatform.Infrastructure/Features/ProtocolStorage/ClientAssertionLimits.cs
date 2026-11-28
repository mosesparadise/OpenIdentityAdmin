namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Defines the bounded identifier, integer time interval and observation used in local RS256 client assertions.</summary>
internal static class ClientAssertionLimits
{
    internal const int LifetimeSeconds = 60;
    internal const int ClockSkewSeconds = 30;
    private const int MinimumIdentifierLength = 16;
    private const int MaximumIdentifierLength = 128;

    /// <summary>Accepts an opaque URL-safe identifier and a finite lifetime whose retention deadline can be represented.</summary>
    internal static bool Valid(string identifier, long issuedAt, long expiresAt, long? notBefore) =>
        identifier.Length is >= MinimumIdentifierLength and <= MaximumIdentifierLength &&
        identifier.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') &&
        issuedAt >= 0 && expiresAt > issuedAt && expiresAt - issuedAt <= LifetimeSeconds && expiresAt <= long.MaxValue - ClockSkewSeconds &&
        (notBefore is null || notBefore >= issuedAt && notBefore <= expiresAt);

    /// <summary>Allows bounded issue skew while enforcing strict expiry and the original not-before time.</summary>
    internal static bool Allows(long issuedAt, long expiresAt, long? notBefore, long now) => now >= 0 && now <= long.MaxValue - ClockSkewSeconds &&
        issuedAt <= now + ClockSkewSeconds && expiresAt > now && (notBefore is null || notBefore <= now);
}
