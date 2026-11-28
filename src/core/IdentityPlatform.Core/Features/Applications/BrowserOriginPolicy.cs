namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Validates the exact serialized HTTPS origins a browser registration may use for cross-origin protocol requests.</summary>
/// <example><code>var allowed = BrowserOriginPolicy.IsValid(["https://portal.example", "https://preview.example:8443"]);</code></example>
public static class BrowserOriginPolicy
{
    public const int MaximumOrigins = 10;
    public const int MaximumOriginLength = 300;

    /// <summary>Requires separately supplied, distinct origins in browser serialization form. Callbacks never imply origin permission.</summary>
    /// <example><code>var valid = BrowserOriginPolicy.IsValid(["https://portal.example"]);</code></example>
    public static bool IsValid(IReadOnlyList<string>? origins) =>
        origins is { Count: > 0 and <= MaximumOrigins } && origins.All(IsOrigin) &&
        origins.Distinct(StringComparer.Ordinal).Count() == origins.Count;

    /// <summary>Compares the input with the scheme and authority a browser sends in its Origin header, including a non-default port.</summary>
    /// <example><code>var valid = BrowserOriginPolicy.IsOrigin("https://portal.example:8443");</code></example>
    public static bool IsOrigin(string? value) =>
        value is { Length: > 0 and <= MaximumOriginLength } && value.All(character => char.IsAscii(character) && !char.IsControl(character)) &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsWellFormedOriginalString() && uri.UserInfo.Length == 0 &&
        uri.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6 &&
        !uri.Host.Contains('*') && !uri.Host.Contains('%') && value == uri.GetLeftPart(UriPartial.Authority);
}
