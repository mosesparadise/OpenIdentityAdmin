using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.AppHost.Tests.Features.LocalDemo;

/// <summary>Separates frozen policy boundaries from progressing browser fixture time without sleeping or weakening native lifetime validation.</summary>
public sealed class BrowserFixtureClockTests
{
    private const int NativeCodeSeconds = 60;
    private const int BeyondOriginalDeadlineSeconds = 61;
    private static readonly DateTimeOffset Initial = DateTimeOffset.FromUnixTimeSeconds(1234567890);

    /// <summary>The default clock remains explicitly controlled when an independent wall-clock source advances.</summary>
    [Fact, Trait("Category", "Harness")]
    public void Given_FrozenFixtureClock_When_IndependentTimeAdvances_Then_OriginalBoundaryRemainsExplicit()
    {
        var source = new FactorClock(Initial);
        var frozen = new FactorClock(Initial);
        source.Now = Initial.AddSeconds(BeyondOriginalDeadlineSeconds);
        Assert.Equal(Initial, frozen.Now);
        Assert.Equal(Initial, frozen.GetUtcNow());
        frozen.Now = source.Now;
        Assert.Equal(source.GetUtcNow(), frozen.GetUtcNow());
    }

    /// <summary>New observations progress while old absolute deadlines stay expired; manual reset cannot silently freeze or renew a live clock.</summary>
    [Fact, Trait("Category", "Harness")]
    public void Given_SourceBackedFixtureClock_When_OriginalDeadlinePasses_Then_FreshObservationsAdvanceWithoutRenewingOldAuthority()
    {
        var source = new FactorClock(Initial);
        var live = new FactorClock(source);
        Assert.Equal(Initial, live.Now);
        var originalDeadline = live.GetUtcNow().AddSeconds(NativeCodeSeconds);
        source.Now = Initial.AddSeconds(BeyondOriginalDeadlineSeconds);
        Assert.Equal(source.Now, live.Now);
        Assert.Equal(source.Now, live.GetUtcNow());
        Assert.True(originalDeadline < live.GetUtcNow());
        var freshDeadline = live.GetUtcNow().AddSeconds(NativeCodeSeconds);
        Assert.Equal(TimeSpan.FromSeconds(NativeCodeSeconds), freshDeadline - source.Now);
        Assert.Throws<InvalidOperationException>(() => live.Now = Initial);
        Assert.Equal(Initial.AddSeconds(BeyondOriginalDeadlineSeconds), source.Now);
        Assert.Equal(source.Now, live.GetUtcNow());
        Assert.Equal(Initial.AddSeconds(NativeCodeSeconds), originalDeadline);
    }
}
