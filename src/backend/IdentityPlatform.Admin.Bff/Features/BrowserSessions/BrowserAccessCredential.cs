using System.Globalization;
using Microsoft.AspNetCore.Authentication;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Holds only an original server-side access credential; possession alone grants no API or browser authority.</summary>
internal sealed class BrowserAccessCredential
{
    private const string AccessTokenName = "access_token";
    private const string ExpirationName = "expires_at";
    private const string TimestampFormat = "O";
    private const int MaximumTokenCharacters = 16_384;
    private const int MaximumLifetimeSeconds = 300;
    internal string Value
    {
        get;
    }
    internal DateTimeOffset ExpiresAt
    {
        get;
    }

    /// <summary>Captures finite immutable transport output without a generated token-bearing string representation.</summary>
    private BrowserAccessCredential(string value, DateTimeOffset expiresAt)
    {
        Value = value;
        ExpiresAt = expiresAt;
    }

    /// <summary>Bounds a credential from the exact native callback; native API validation remains mandatory before data access.</summary>
    internal static BrowserAccessCredential? Capture(string? value, DateTimeOffset? expiresAt, DateTimeOffset now) =>
        value is { Length: > 0 and <= MaximumTokenCharacters } && value.All(IsTokenCharacter) &&
        expiresAt is { Offset.Ticks: 0 } expiry && expiry > now && expiry - now <= TimeSpan.FromSeconds(MaximumLifetimeSeconds)
            ? new(value, expiry) : null;

    /// <summary>Accepts only bounded bearer transport characters, excluding whitespace, controls and non-ASCII header content.</summary>
    private static bool IsTokenCharacter(char value) => char.IsAsciiLetterOrDigit(value) || value is '-' or '.' or '_' or '~' or '+' or '/' or '=';

    /// <summary>Stores only access and its original expiration inside the subsequently protected native ticket.</summary>
    internal void Store(AuthenticationProperties properties) => properties.StoreTokens(
        [new() { Name = AccessTokenName, Value = Value },
         new() { Name = ExpirationName, Value = ExpiresAt.ToString(TimestampFormat, CultureInfo.InvariantCulture) }]);

    /// <summary>Rejects legacy, malformed and expired protected state without inferring an expiry or renewing the session.</summary>
    internal static BrowserAccessCredential? Read(AuthenticationProperties properties, DateTimeOffset now)
    {
        var raw = properties.GetTokenValue(ExpirationName);
        return DateTimeOffset.TryParseExact(raw, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry) &&
            expiry.Offset == TimeSpan.Zero && raw == expiry.ToString(TimestampFormat, CultureInfo.InvariantCulture)
                ? Capture(properties.GetTokenValue(AccessTokenName), expiry, now) : null;
    }
}
