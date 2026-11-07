using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

/// <summary>Orders genuine initial replay across distinct issuer processes using only real provider locks and pinned HTTPS transports.</summary>
[Collection("distribution")]
public sealed class PilotReplayDistributionTests(DistributionFixture fixture)
{
    /// <summary>Retains both winner/replay directions as distinct native cases for each supported provider.</summary>
    public static IEnumerable<TheoryDataRow<string, string, string>> Directions => DistributionEvidence.Directions;

    /// <summary>The first competing replay leaves no winning linked entry usable without relying on a second cleanup request.</summary>
    [Theory, MemberData(nameof(Directions))]
    public async Task Given_DurablyConsumedPilotCodeOnOneProcess_When_InitialReplayWaitsOnAnother_Then_NoWinningEntrySurvivesReplay(
        string provider, string source, string target)
    {
        var environment = await fixture.GetAsync(provider);
        await PilotReplayDistributionJourney.RunAsync(environment, provider, source, target, nativeRead: false);
    }

    /// <summary>Explicit synthetic SQL row locking must exercise exact native-read matching in both real-process directions.</summary>
    [Theory]
    [InlineData("SqlServer", "auth", "auth-secondary", Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "auth-secondary", "auth", Traits = ["Provider", "SqlServer"])]
    public async Task Given_SyntheticConsumedCodeReadLock_When_InitialReplayWaitsOnPeerProcess_Then_ExactNativeReadMatchRevokesWinningEntries(
        string provider, string source, string target)
    {
        var environment = await fixture.GetAsync(provider);
        await PilotReplayDistributionJourney.RunAsync(environment, provider, source, target, nativeRead: true);
    }
}
