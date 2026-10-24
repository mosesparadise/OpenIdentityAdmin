using IdentityPlatform.Infrastructure.Features.Sessions.Browser;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Freezes the original local proof deadline for final admission after all scope disposal and before any response body is sent.</summary>
internal sealed class BrowserLoginPublication(long created, long expires, BrowserLoginPublication? previous = null)
{
    /// <summary>Rejects late expiry and excessive rollback without granting a new lifetime after awaited local work.</summary>
    internal void Require(TimeProvider clock)
    {
        previous?.Require(clock);
        var now = clock.GetUtcNow().UtcTicks;
        if (now < created - BrowserSessionPolicy.Tolerance || Math.Max(created, now) >= expires)
        {
            throw new InvalidOperationException(BrowserLoginBoundary.Unavailable);
        }
    }
}
