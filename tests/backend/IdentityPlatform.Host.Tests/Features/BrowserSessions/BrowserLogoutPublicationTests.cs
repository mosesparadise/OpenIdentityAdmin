using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Separately proves final cookie lifetime calculation and preservation of shorter original publication bounds.</summary>
public sealed class BrowserLogoutPublicationTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private const int OriginalSeconds = 10;
    private const int GenerationSeconds = 300;
    private const int DelaySeconds = 5;
    private const string SyntheticProof = "synthetic-protected-proof";
    private const string RemainingAge = "max-age=295";

    /// <summary>Elapsed remote work reduces the positive cookie Max-Age while the server's original immutable deadline is preserved.</summary>
    [Fact]
    public void Given_DelayedRemotePreparation_When_GenerationCookieIsPublished_Then_MaxAgeUsesOnlyRemainingLifetime()
    {
        var http = new DefaultHttpContext();
        var clock = new BffSessionClock(Start.AddSeconds(DelaySeconds));
        http.Features.Set(new BrowserLoginPublication(Start.UtcTicks, Start.AddSeconds(OriginalSeconds).UtcTicks));
        new BrowserLogoutCookie(SyntheticProof, Start.AddSeconds(GenerationSeconds).UtcTicks).Publish(http, clock);
        Assert.Contains(RemainingAge, http.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    /// <summary>The earlier incoming proof can expire independently of the new tombstone; the later positive cookie must then be withheld.</summary>
    [Fact]
    public void Given_ExpiredOriginalProof_When_LaterGenerationCookieIsStillLive_Then_PublicationDeniesWithoutCookie()
    {
        var http = new DefaultHttpContext();
        var clock = new BffSessionClock(Start.AddSeconds(OriginalSeconds));
        http.Features.Set(new BrowserLoginPublication(Start.UtcTicks, Start.AddSeconds(OriginalSeconds).UtcTicks));
        Assert.Throws<InvalidOperationException>(() => new BrowserLogoutCookie(SyntheticProof, Start.AddSeconds(GenerationSeconds).UtcTicks).Publish(http, clock));
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
    }
}
