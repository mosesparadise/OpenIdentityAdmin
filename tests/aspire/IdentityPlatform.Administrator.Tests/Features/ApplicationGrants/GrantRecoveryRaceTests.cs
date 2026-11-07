using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Proves actual delayed failure capture cannot erase peer progress, supersede fresh approval or replace terminal success.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantRecoveryRaceTests(BootstrapFixture fixture)
{
    private const int ProgressAndStopRevisions = 2;
    /// <summary>A real failed native batch drains before its fresh failure recorder waits on and reconciles the peer's actual committed transition.</summary>
    [Theory]
    [InlineData("PostgreSql", GrantRecoveryRace.Progress, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", GrantRecoveryRace.Approval, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", GrantRecoveryRace.Success, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", GrantRecoveryRace.Progress, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", GrantRecoveryRace.Approval, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", GrantRecoveryRace.Success, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ActualFailedBatch_When_PeerCommitsBeforeFailureRecording_Then_DurableProgressApprovalAndSuccessAreReconciled(string provider, string outcome)
    {
        using var fault = new GrantFailureCapture();
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, fault);
        var winner = new GrantAuthorityRaceProbe(holdCommit: true);
        await using var peer = test.Fixture.Peer(provider, winner);
        var population = outcome == GrantRecoveryRace.Success ? 1 : GrantAuthorityEvidence.Population;
        await test.SeedAsync(test.Id, population, population);
        var control = await GrantAuthorityEvidence.ControlAsync(test);
        var native = await GrantAuthorityEvidence.ReadAsync(test, test.Id);
        var other = await GrantAuthorityEvidence.ReadAsync(test, control);
        var accepted = await test.AdmitAsync(test.Intent());
        var original = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        string? peerState = null;
        winner.BeforeCommit = database => peerState = ApplicationUpdateSnapshot.Hash(database.ChangeTracker.Entries<ApplicationGrantJob>().Single().Entity);
        var before = await GrantRecoveryRace.RunAsync(test, provider, accepted.Id, outcome, fault, winner, peer);
        var after = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        after.Preserves(original);
        Require(before, after, outcome, peerState);
        var changed = outcome == GrantRecoveryRace.Progress ? GrantAuthorityEvidence.Batch : outcome == GrantRecoveryRace.Success ? 1 : 0;
        (await GrantAuthorityEvidence.ReadAsync(test, test.Id)).Require(native, changed);
        (await GrantAuthorityEvidence.ReadAsync(test, control)).Require(other, 0);
        await RequireNextAsync(test, accepted.Id, outcome, after);
    }

    /// <summary>Reconciles every committed audit and exact peer snapshot; only same-approval incomplete work may receive one extra zero-delta stop.</summary>
    private static void Require(GrantAuditWitness before, GrantAuditWitness after, string outcome, string? peerState)
    {
        Assert.NotNull(peerState);
        after.Require();
        after.Preserves(before);
        var stopped = outcome == GrantRecoveryRace.Progress;
        Assert.Equal(before.Details.Length + (stopped ? ProgressAndStopRevisions : 1), after.Details.Length);
        Assert.Equal(before.Approvals.Length + (outcome == GrantRecoveryRace.Approval ? 1 : 0), after.Approvals.Length);
        if (stopped)
        {
            Assert.Equal(GrantJobState.Stopped, after.Job.State);
            Assert.Equal(GrantJobFailure.Storage, after.Job.Failure);
            Assert.Equal(GrantAuthorityEvidence.Batch, after.Job.AuthorizationCount);
            Assert.Equal(GrantAuthorityEvidence.Batch, after.Job.TokenCount);
            Assert.Equal(before.Job.EffectiveApprovalId, after.Job.EffectiveApprovalId);
        }
        else
        {
            Assert.Equal(peerState, ApplicationUpdateSnapshot.Hash(after.Job));
            Assert.Equal(outcome == GrantRecoveryRace.Approval ? GrantJobState.Queued : GrantJobState.Succeeded, after.Job.State);
            Assert.Equal(outcome != GrantRecoveryRace.Approval, before.Job.EffectiveApprovalId == after.Job.EffectiveApprovalId);
        }
    }

    /// <summary>Fresh approval remains runnable while stopped/successful work cannot gain an extra revision from another poll.</summary>
    private static async Task RequireNextAsync(GrantTestContext test, string id, string outcome, GrantAuditWitness before)
    {
        Assert.Equal(outcome == GrantRecoveryRace.Approval, await test.BatchAsync(id));
        var after = await GrantAuditWitness.ReadAsync(test, id);
        after.Require();
        after.Preserves(before);
        if (outcome == GrantRecoveryRace.Approval)
        {
            Assert.Equal(GrantJobState.Processing, after.Job.State);
            Assert.Equal(GrantAuthorityEvidence.Batch, after.Job.AuthorizationCount);
            Assert.Equal(GrantAuthorityEvidence.Batch, after.Job.TokenCount);
        }
        else
        {
            Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Job), ApplicationUpdateSnapshot.Hash(after.Job));
            Assert.Equal(before.Details.Length, after.Details.Length);
        }
    }
}
