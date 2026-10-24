using IdentityPlatform.Infrastructure.Features.Sessions.Browser;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Seeds bounded independent database states to exercise exact capacity and retention boundaries without thousands of login ceremonies.</summary>
internal static class BrowserCoordinationSeeds
{
    private const int ExpiredMinutes = 10;
    private const int IntentMinutes = 5;
    private const int ToleranceSeconds = 30;
    private const string TargetSubject = "synthetic-correlation-target";
    private const string TargetSession = "synthetic-correlation-source";

    internal const int Live = 1024;
    internal const int Retained = 10000;
    internal const int Batch = 100;

    /// <summary>Creates one inert test context with independently specified UTC tick bounds.</summary>
    internal static BrowserContext Context(long now, bool expired = false)
    {
        var start = expired ? now - TimeSpan.FromMinutes(ExpiredMinutes).Ticks : now;
        return new BrowserContext
        {
            Digest = BrowserCoordinationFixture.Digest(),
            CorrelationId = Guid.NewGuid(),
            Generation = 1,
            Revision = 1,
            ObservedAt = start,
            ExpiresAt = start + TimeSpan.FromMinutes(IntentMinutes).Ticks,
            RetainUntil = start + TimeSpan.FromMinutes(IntentMinutes).Ticks + TimeSpan.FromSeconds(ToleranceSeconds).Ticks
        };
    }

    /// <summary>Creates only intent metadata; these fixture rows never supply native callback or authenticated actor evidence.</summary>
    internal static BrowserLoginIntent Intent(BrowserContext browser, bool live = true) => new()
    {
        Digest = BrowserCoordinationFixture.Digest(),
        BrowserDigest = browser.Digest,
        CorrelationId = browser.CorrelationId,
        Generation = browser.Generation,
        Revision = 1,
        CreatedAt = browser.ObservedAt,
        ObservedAt = browser.ObservedAt,
        ExpiresAt = browser.ExpiresAt,
        RetainUntil = browser.RetainUntil,
        State = live ? BrowserIntentState.Pending : BrowserIntentState.Denied
    };

    /// <summary>Creates remote-correlation capacity only, with synthetic target metadata and no issuer authority.</summary>
    internal static BrowserLogoutContinuation Logout(BrowserContext browser, bool live = true) => new()
    {
        Digest = BrowserCoordinationFixture.Digest(),
        BrowserDigest = browser.Digest,
        CorrelationId = browser.CorrelationId,
        Generation = browser.Generation,
        Revision = 1,
        CreatedAt = browser.ObservedAt,
        ObservedAt = browser.ObservedAt,
        ExpiresAt = browser.ExpiresAt,
        RetainUntil = browser.RetainUntil,
        State = live ? BrowserLogoutState.RemotePending : BrowserLogoutState.ReturnedUnverified,
        Subject = TargetSubject,
        SourceSession = TargetSession
    };
}
