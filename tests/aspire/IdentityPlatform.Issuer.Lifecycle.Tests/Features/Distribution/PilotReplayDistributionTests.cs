using System.Runtime.ExceptionServices;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

/// <summary>Orders genuine initial replay across distinct issuer processes using only real provider locks and pinned HTTPS transports.</summary>
[Collection("distribution")]
public sealed class PilotReplayDistributionTests(DistributionFixture fixture)
{
    private const string DistinctProcesses = "Ordered replay requires distinct actual issuer processes.";
    private const string RetainedProcesses = "The ordered flow must retain both original process generations.";
    /// <summary>Retains both winner/replay directions as distinct native cases for each supported provider.</summary>
    public static IEnumerable<TheoryDataRow<string, string, string>> Directions => DistributionEvidence.Directions;

    /// <summary>The first competing replay leaves no winning linked entry usable without relying on a second cleanup request.</summary>
    [Theory, MemberData(nameof(Directions))]
    public async Task Given_DurablyConsumedPilotCodeOnOneProcess_When_InitialReplayWaitsOnAnother_Then_NoWinningEntrySurvivesReplay(
        string provider, string source, string target)
    {
        var environment = await fixture.GetAsync(provider);
        var generations = environment.CurrentNodeGenerations();
        Assert.True(generations[source] != generations[target], DistinctProcesses);
        using var browser = environment.NodeClient(source, autoRedirect: true);
        using var winnerTransport = environment.NodeClient(source);
        using var replayTransport = environment.NodeClient(target);
        var control = await ExchangeEvidence.IssueAsync(environment, browser);
        var evidence = await ExchangeEvidence.IssueAsync(environment, browser);
        var expiration = await PilotReplayDistributionEvidence.ExpirationAsync(environment, evidence);
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        var barrier = await PilotReplayDatabaseBarrier.CreateAsync(environment, provider, evidence.AuthorizationId,
            generations[source].Id, generations[target].Id);
        Task<HttpResponseMessage>? winner = null;
        Task<HttpResponseMessage>? replay = null;
        ExceptionDispatchInfo? failure = null;
        try
        {
            var token = TestContext.Current.CancellationToken;
            await evidence.RequireFreshAsync(environment);
            winner = environment.RelyingParty.ExchangeAsync(evidence.Flow.Code, evidence.Flow.Verifier, transport: winnerTransport, cancellationToken: token);
            var session = await barrier.WaitForWinnerAsync(winner, token);
            await PilotReplayDistributionEvidence.RequireConsumedAsync(environment, evidence, expiration);
            replay = environment.RelyingParty.ExchangeAsync(evidence.Flow.Code, evidence.Flow.Verifier, transport: replayTransport, cancellationToken: token);
            await barrier.WaitForReplayAsync(session, winner, replay, token);
            await barrier.ReleaseAsync(token);
            await Task.WhenAll(winner, replay);
            await PilotReplayDistributionEvidence.RequireOutcomeAsync(environment, evidence, expiration, winner.Result, replay.Result, winnerTransport, before);
            await PilotReplayDistributionEvidence.RequireControlAsync(environment, control, evidence.AuthorizationId, target);
            var current = environment.CurrentNodeGenerations();
            Assert.True(generations.All(value => current[value.Key] == value.Value), RetainedProcesses);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        await PilotReplayDistributionCleanup.CompleteAsync(environment, barrier, winner, replay, failure);
    }
}
