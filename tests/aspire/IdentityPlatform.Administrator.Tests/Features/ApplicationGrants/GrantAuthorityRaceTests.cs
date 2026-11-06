using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Orders genuine permission removal against a real worker batch through exact provider-observed waits and actual transaction commit callbacks.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantAuthorityRaceTests(BootstrapFixture fixture)
{
    /// <summary>Governance-first stops without native changes; batch-first retains exactly one committed batch before subsequent authority loss stops the remainder.</summary>
    [Theory]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    public async Task Given_IndependentWorkerAndGovernanceNodes_When_PermissionRemovalCommitsInEitherObservedOrder_Then_OnlyAuthorizedProgressPersists(string provider, bool batchFirst)
    {
        var worker = new GrantAuthorityRaceProbe(batchFirst);
        var governance = new GrantAuthorityRaceProbe(!batchFirst);
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, worker);
        await test.SeedAsync(test.Id, GrantAuthorityEvidence.Population, GrantAuthorityEvidence.Population);
        var control = await GrantAuthorityEvidence.ControlAsync(test);
        var native = await GrantAuthorityEvidence.ReadAsync(test, test.Id);
        var peer = await GrantAuthorityEvidence.ReadAsync(test, control);
        var accepted = await test.AdmitAsync(test.Intent());
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        await using var removal = await GrantAuthorityRemoval.PrepareAsync(test, provider, governance);
        await RaceAsync(test, provider, accepted.Id, removal, worker, governance, batchFirst);
        await removal.RequireAsync();
        if (batchFirst)
        {
            var progress = await GrantAuditWitness.ReadAsync(test, accepted.Id);
            progress.Require();
            progress.Preserves(before);
            Assert.Equal(before.Details.Length + 1, progress.Details.Length);
            Assert.Single(progress.Approvals);
            Assert.Equal(GrantJobState.Processing, progress.Job.State);
            Assert.Equal(GrantAuthorityEvidence.Batch, progress.Job.AuthorizationCount);
            Assert.Equal(GrantAuthorityEvidence.Batch, progress.Job.TokenCount);
            before = progress;
            Assert.True(await test.BatchAsync(accepted.Id));
        }
        await GrantAuthorityEvidence.RequireStopAsync(test, accepted.Id, before);
        (await GrantAuthorityEvidence.ReadAsync(test, test.Id)).Require(native, batchFirst ? GrantAuthorityEvidence.Batch : 0);
        (await GrantAuthorityEvidence.ReadAsync(test, control)).Require(peer, 0);
    }

    /// <summary>Holds the chosen winner immediately before native commit and proves the exact peer session blocks on its governance lock.</summary>
    private static async Task RaceAsync(GrantTestContext test, string provider, string id, GrantAuthorityRemoval removal,
        GrantAuthorityRaceProbe worker, GrantAuthorityRaceProbe governance, bool batchFirst)
    {
        worker.Armed = governance.Armed = true;
        // Starts exactly one worker operation with the prepared durable job identity.
        Task<bool> BatchAsync() => test.BatchAsync(id);
        Func<Task<bool>> revoke = removal.ExecuteAsync;
        var winner = batchFirst ? worker : governance;
        var loser = batchFirst ? governance : worker;
        var lease = test.Fixture.Scenario.Account.Journey.Lease;
        var observer = new NativeLockObservation(provider, lease.AdministratorConnection);
        var results = await NativeBlockedRace.InvokeAsync(batchFirst ? BatchAsync : revoke, winner.Committing.Task,
            batchFirst ? revoke : BatchAsync, loser.Lock.Task, winner.Release, observer.WaitAsync);
        Assert.All(results, result => Assert.True(result));
        worker.Require(batchFirst, !batchFirst);
        governance.Require(true, false);
    }
}
