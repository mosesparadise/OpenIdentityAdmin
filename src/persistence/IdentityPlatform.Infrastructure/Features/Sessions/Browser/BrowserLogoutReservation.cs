namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Detaches authenticated source provenance and immutable original bounds from an actual committed logout transition.</summary>
internal sealed class BrowserLogoutReservation
{
    internal string BrowserDigest { get; private init; } = string.Empty;
    internal string Digest { get; private init; } = string.Empty;
    internal string SourceSession { get; private init; } = string.Empty;
    internal long Generation
    {
        get; private init;
    }
    internal long CreatedAt
    {
        get; private init;
    }
    internal long ObservedAt
    {
        get; private init;
    }
    internal long ExpiresAt
    {
        get; private init;
    }

    /// <summary>Copies the exact stored values while the owning transaction is still available; callers release it before publishing.</summary>
    internal static BrowserLogoutReservation Capture(BrowserLogoutContinuation row) => new()
    {
        BrowserDigest = row.BrowserDigest,
        Digest = row.Digest,
        SourceSession = row.SourceSession,
        Generation = row.Generation,
        CreatedAt = row.CreatedAt,
        ObservedAt = row.ObservedAt,
        ExpiresAt = row.ExpiresAt
    };
}
