using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Requires original product deadlines and terminal publication failure independently of the finite HTTP request budget.</summary>
public sealed class BrowserLoginPublicationTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private const int FirstExpiry = 10;
    private const int NewExpiry = 20;

    /// <summary>A later new-intent lifetime cannot replace the original generation or pending proof's publication boundary.</summary>
    [Fact]
    public void Given_ChainedOriginalProof_When_OlderDeadlineIsReached_Then_NewerLifetimeCannotPublish()
    {
        var clock = new BffSessionClock(Start);
        var prior = new BrowserLoginPublication(Start.UtcTicks, Start.AddSeconds(FirstExpiry).UtcTicks);
        var newer = new BrowserLoginPublication(Start.UtcTicks, Start.AddSeconds(NewExpiry).UtcTicks, prior);
        newer.Require(clock);
        clock.Set(Start.AddSeconds(FirstExpiry));
        Assert.Throws<InvalidOperationException>(() => newer.Require(clock));
    }

    /// <summary>A swallowed guard exception permanently fails the original owner even if a later guard invocation would succeed.</summary>
    [Fact]
    public void Given_PublicationGuardDenial_When_CallerCatchesAndRetries_Then_RequestRemainsFailed()
    {
        var clock = new BffSessionClock(Start);
        var fail = true;
        var request = new ProtocolStorageRequest(clock, TestContext.Current.CancellationToken, () =>
        {
            if (fail)
            {
                throw new InvalidOperationException();
            }
        });
        Assert.Throws<InvalidOperationException>(() => request.RequirePublication(ProtocolResponseOutcome.ReadOnly));
        fail = false;
        Assert.Throws<InvalidOperationException>(() => request.RequirePublication(ProtocolResponseOutcome.ReadOnly));
    }
}
