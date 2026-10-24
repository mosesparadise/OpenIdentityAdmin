namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Defines immutable operation deadlines, finite quotas and canonical persistence bindings.</summary>
internal static class BrowserSessionPolicy
{
    internal const int LiveLimit = 1024;
    internal const int RetainedLimit = 10000;
    internal const int CleanupLimit = 100;
    internal const int DigestLength = 64;
    internal const long Lifetime = 300 * TimeSpan.TicksPerSecond;
    internal const long Tolerance = 30 * TimeSpan.TicksPerSecond;

    /// <summary>Rejects aliases and malformed digests before they reach provider equality or indexing.</summary>
    internal static bool IsDigest(string? value) => value is { Length: DigestLength } &&
        value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');

    /// <summary>Checks immutable bounds and monotonic effective time, denying expiry equality and excessive clock rollback.</summary>
    internal static bool Current(long created, long observed, long expires, long now) =>
        created > 0 && observed >= created && expires > created && expires - created <= Lifetime &&
        now >= observed - Tolerance && Math.Max(now, observed) < expires;

    /// <summary>Requires the exact current browser generation without refreshing its original operation or ticket deadline.</summary>
    internal static bool Matches(BrowserContext row, long generation, long now) => generation > 0 &&
        row.Generation == generation && row.CorrelationId != Guid.Empty && row.Revision > 0 && row.ObservedAt > 0 &&
        now >= row.ObservedAt - Tolerance && Math.Max(now, row.ObservedAt) < row.ExpiresAt;
}

/// <summary>Terminal intent states never transition back to pending or reserve another native challenge.</summary>
internal enum BrowserIntentState
{
    Pending = 1, Reserved = 2, Established = 3, Denied = 4, Cancelled = 5
}

/// <summary>A return records navigation only, never a verified issuer revocation.</summary>
internal enum BrowserLogoutState
{
    LocalCommitted = 1, RemotePending = 2, ReturnedUnverified = 3, Incomplete = 4
}

/// <summary>Names the bounded transitions represented by committed local audit intent.</summary>
internal enum BrowserMutation
{
    Start = 1, Reserve = 2, Establish = 3, Deny = 4, Logout = 5, RemotePending = 6, Return = 7
}
