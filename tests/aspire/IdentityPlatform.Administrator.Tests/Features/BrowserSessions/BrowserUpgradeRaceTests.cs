using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires an independently observed real lock wait for concurrent explicit upgrade reconciliation.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserUpgradeRaceTests(BootstrapFixture fixture)
{
    /// <summary>Two independent roots contend at the real governance lock; exactly one transition commits and its peer performs a write-free replay.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_SameUpgradeOnTwoRoots_When_ThePeerWaitsOnActualOwnership_Then_OnlyOneRevisionAndAuditCommit(string provider)
    {
        var winner = new BrowserUpgradeRaceProbe(true);
        var second = new BrowserUpgradeRaceProbe(false);
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider, configure: winner.Configure);
        await using var peer = scenario.CreatePeer(provider, second.Configure);
        _ = await NativeAccountFlow.PushAsync(scenario, peer.Services);
        var input = BrowserUpgradeFixture.Input(scenario);
        var observer = new NativeLockObservation(provider, scenario.Account.Journey.Lease.AdministratorConnection);
        winner.Armed = true;
        second.Armed = true;
        try
        {
            var results = await NativeBlockedRace.InvokeAsync(() => BrowserUpgradeFixture.UpgradeAsync(scenario, input), winner.Reached.Task,
                () => BrowserUpgradeFixture.UpgradeAsync(scenario, input, peer.Services), second.Reached.Task, winner.Release, observer.WaitAsync);
            Assert.True(results[0]);
            Assert.False(results[1]);
            Assert.Equal(1, winner.Commits);
            Assert.Equal(0, second.Commits);
        }
        finally { winner.Armed = false; second.Armed = false; }
        await BrowserUpgradeEffects.RequireReceiptAsync(scenario, input);
        var final = await BrowserUpgradeFixture.ReadAsync(scenario);
        Assert.Equal(2, final.Revision);
        Assert.True(final.Browser && !final.Legacy);
        Assert.False(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
        Assert.True(final == await BrowserUpgradeFixture.ReadAsync(scenario));
    }
}
