using System.ComponentModel.DataAnnotations;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Projects only safe native registration metadata, never credentials, keys or arbitrary native properties.</summary>
/// <example><code>var application = new ApplicationSummary("opaque-id", "example-web", "Example", "confidential");</code></example>
public sealed record ApplicationSummary(
    [property: MinLength(1), MaxLength(ApplicationProjection.IdentifierLength)] string Id,
    [property: MinLength(1), MaxLength(ApplicationProjection.ClientIdentifierLength)] string ClientId,
    [property: MaxLength(ApplicationProjection.DisplayNameLength)] string? DisplayName,
    [property: MaxLength(ApplicationProjection.ClientTypeLength)] string? ClientType);

/// <summary>Returns a finite stable page and one lookahead flag without claiming a cross-request snapshot or separate total count.</summary>
/// <example><code>var page = new ApplicationPage([], 1, 25, false, originalCredentialExpiry);</code></example>
public sealed record ApplicationPage(
    [property: MaxLength(ApplicationListQuery.MaximumSize)] IReadOnlyList<ApplicationSummary> Items,
    [property: Range(1, ApplicationListQuery.MaximumPage)] int Page,
    [property: Range(1, ApplicationListQuery.MaximumSize)] int PageSize,
    bool HasNextPage, DateTimeOffset ExpiresAt);

/// <summary>Bounds projected native text independently of provider schema and JSON serialization overhead.</summary>
/// <example><code>if (responseBytes.Length &gt; ApplicationProjection.MaximumResponseBytes) throw new InvalidOperationException();</code></example>
public static class ApplicationProjection
{
    public const int IdentifierLength = 400;
    public const int ClientIdentifierLength = 100;
    public const int DisplayNameLength = 200;
    public const int ClientTypeLength = 50;
    public const int MaximumResponseBytes = 524_288;
    public const int MaximumQueryCharacters = 2048;

    /// <summary>Checks the complete safe projection after bounded SQL materialization or untrusted downstream parsing.</summary>
    /// <example><code>var valid = ApplicationProjection.IsValid(new("id", "client", null, null));</code></example>
    public static bool IsValid(ApplicationSummary value) => value.Id is { Length: > 0 and <= IdentifierLength } &&
        value.ClientId is { Length: > 0 and <= ClientIdentifierLength } &&
        value.DisplayName is null or { Length: <= DisplayNameLength } && value.ClientType is null or { Length: <= ClientTypeLength };
}
