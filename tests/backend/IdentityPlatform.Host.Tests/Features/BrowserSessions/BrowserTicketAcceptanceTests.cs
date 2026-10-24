using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Separately challenges protected ticket authority and durable generation eligibility on every simulated read.</summary>
public sealed class BrowserTicketAcceptanceTests
{
    private const int NextGeneration = 2;
    private const int IdleMinutes = 5;

    /// <summary>A valid generation cannot authenticate a bad ticket, and a valid ticket cannot survive stale or absent browser binding.</summary>
    [Theory]
    [InlineData(BrowserTicketFault.None, true)]
    [InlineData(BrowserTicketFault.StaleGeneration, false)]
    [InlineData(BrowserTicketFault.OtherBrowser, false)]
    [InlineData(BrowserTicketFault.OtherHandle, false)]
    [InlineData(BrowserTicketFault.MissingBrowser, false)]
    [InlineData(BrowserTicketFault.MissingTicket, false)]
    [InlineData(BrowserTicketFault.Expired, false)]
    [InlineData(BrowserTicketFault.Revoked, false)]
    [InlineData(BrowserTicketFault.Corrupt, false)]
    public void Given_TicketAndGeneration_When_EveryReadIsEvaluated_Then_BothIndependentBoundariesMustPassWithoutRenewal(BrowserTicketFault fault, bool expected)
    {
        using var fixture = new BffSessionFixture();
        using var protection = fixture.Protection();
        var codec = new SessionTicketCodec(protection.Provider);
        var handle = SessionHandle.Create();
        var row = BffSessionFixture.Row();
        row.Digest = handle.Digest;
        row.ProtectedTicket = codec.Protect(row, BffSessionFixture.Ticket());
        var digest = SessionHandle.Create().Digest;
        var browser = Browser(digest, row);
        if (fault == BrowserTicketFault.Revoked)
        {
            row.RevokedAt = row.CreatedAt;
        }
        if (fault == BrowserTicketFault.Corrupt)
        {
            row.ProtectedTicket[0] ^= 1;
        }
        var now = fault == BrowserTicketFault.Expired ? BffSessionFixture.Start.AddMinutes(IdleMinutes) : BffSessionFixture.Start;
        var accepted = BrowserTicketAcceptance.Accept(fault == BrowserTicketFault.OtherHandle ? SessionHandle.Create().Handle : handle.Handle,
            fault == BrowserTicketFault.OtherBrowser ? SessionHandle.Create().Digest : digest,
            fault == BrowserTicketFault.StaleGeneration ? NextGeneration : 1, fault == BrowserTicketFault.MissingBrowser ? null : browser,
            fault == BrowserTicketFault.MissingTicket ? null : row, codec, now);
        Assert.Equal(expected, accepted);
        Assert.Equal(BffSessionFixture.Start.UtcTicks, row.LastActivityAt);
        Assert.Equal(BffSessionFixture.Start.UtcTicks, browser.ObservedAt);
        Assert.Equal(1, browser.Generation);
        Assert.Equal(1, row.Revision);
    }
    /// <summary>Constructs a positive browser binding independently of the acceptance predicate.</summary>
    private static BrowserContext Browser(string digest, IdentityPlatform.Infrastructure.Features.Sessions.Persistence.AdminSessionTicket row) => new()
    {
        Digest = digest,
        TicketDigest = row.Digest,
        Generation = 1,
        Revision = 1,
        CorrelationId = Guid.NewGuid(),
        ObservedAt = row.CreatedAt,
        ExpiresAt = row.AbsoluteExpiresAt
    };

}

/// <summary>Names independently invalid ticket and browser-state boundaries.</summary>
public enum BrowserTicketFault
{
    None, StaleGeneration, OtherBrowser, OtherHandle, MissingBrowser, MissingTicket, Expired, Revoked, Corrupt
}
