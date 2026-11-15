using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Requires durable retired-job recovery without altering original receipt/history or ever making the registration editable again.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationRetirementRecoveryTests(BootstrapFixture fixture)
{
    private const string Revoke = "grants.revoke";
    private const string Disable = "applications.disable";
    private const string Reason = "security";
    private const string GuidFormat = "D";
    private const int MultipleBatches = 9;
    private const int RestartBatches = 33;
    private const int PartialCount = 8;
    private const int Conflict = 409;
    private const int Stale = 412;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ObservationInterval = TimeSpan.FromMilliseconds(25);

    /// <summary>Restored worker authority cannot resume stopped work; explicit Continue needs current versions but never re-retires or rewrites the original receipt.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RetiredPartialJob_When_PermissionIsRestored_Then_OnlyExplicitExactContinuationResumesAndOriginalRetirementReplayObservesIt(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        await test.Grants.SeedAsync(test.Id, MultipleBatches, MultipleBatches);
        var intent = test.Intent();
        var accepted = await test.RetireAsync(intent);
        var receipt = ApplicationUpdateSnapshot.Hash(await test.ReceiptAsync());
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        var claims = await ApplicationRetirementAuthorityTests.RemoveAsync(test, Revoke);
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        var stopped = await test.Grants.ReadAsync(accepted.Job.Id);
        Assert.Equal(GrantJobState.Stopped, stopped.State);
        Assert.Equal(GrantJobFailure.Authority, stopped.Failure);
        Assert.Equal(PartialCount, stopped.TokenCount);
        var history = await GrantAuditWitness.ReadAsync(test.Grants, accepted.Job.Id);
        await ApplicationRetirementAuthorityTests.RestoreAsync(test, claims);
        Assert.False(await test.Grants.BatchAsync(accepted.Job.Id));
        var continueIntent = Continue(test, accepted.ApplicationVersion, stopped);
        Assert.Equal(Stale, (await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.Grants.AdmitAsync(continueIntent with { Version = intent.Version }))).Status);
        Assert.Equal(Stale, (await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.Grants.AdmitAsync(continueIntent with { JobVersion = accepted.Job.Version }))).Status);
        var continued = await test.Grants.AdmitAsync(continueIntent);
        Assert.Equal(accepted.Job.Id, continued.Id);
        var replay = await test.RetireAsync(intent);
        Assert.Equal(continued.Version, replay.Job.Version);
        Assert.Equal(accepted.ApplicationVersion, replay.ApplicationVersion);
        Assert.Equal(receipt, ApplicationUpdateSnapshot.Hash(await test.ReceiptAsync()));
        Assert.True(await test.Grants.BatchAsync(continued.Id));
        Assert.True(await test.Grants.BatchAsync(continued.Id));
        var final = await GrantAuditWitness.ReadAsync(test.Grants, continued.Id);
        final.Require();
        final.Preserves(history);
        Assert.Equal(GrantJobState.Succeeded, final.Job.State);
        Assert.Equal(MultipleBatches, final.Job.TokenCount);
        Assert.Equal(receipt, ApplicationUpdateSnapshot.Hash(await test.ReceiptAsync()));
        Assert.Equal(Conflict, (await test.Fixture.ExecuteAsync(ApplicationMutationCommand.Lifecycle(test.Id, accepted.ApplicationVersion, new(Reason, true), true)!)).Status);
    }

    /// <summary>Lifecycle permission is needed to retire but is not an ongoing worker or Continue requirement after the terminal decision is committed.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_AcceptedRetirement_When_LifecyclePermissionIsRemoved_Then_CurrentGrantAuthorityStillFinishesTheRecordedJob(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        var intent = test.Intent();
        var accepted = await test.RetireAsync(intent);
        await ApplicationRetirementAuthorityTests.RemoveAsync(test, Disable);
        Assert.Equal(403, (await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.RetireAsync(intent))).Status);
        var claims = await ApplicationRetirementAuthorityTests.RemoveAsync(test, Revoke);
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        var stopped = await test.Grants.ReadAsync(accepted.Job.Id);
        Assert.Equal(GrantJobState.Stopped, stopped.State);
        await ApplicationRetirementAuthorityTests.RestoreAsync(test, claims);
        await test.Grants.AdmitAsync(Continue(test, accepted.ApplicationVersion, stopped));
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        Assert.Equal(GrantJobState.Succeeded, (await test.Grants.ReadAsync(accepted.Job.Id)).State);
        Assert.True((await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id)).Policy.Retired);
    }

    /// <summary>Actual hosted Worker shutdown drains partial progress and a new host finishes the same retired job from the same database.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RetiredQueuedWork_When_HostedWorkerRestartsAfterProgress_Then_TheSameReceiptAndApprovalComplete(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        await test.Grants.SeedAsync(test.Id, RestartBatches, RestartBatches);
        var accepted = await test.RetireAsync(test.Intent());
        var receipt = ApplicationUpdateSnapshot.Hash(await test.ReceiptAsync());
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(test.Token);
        deadline.CancelAfter(Deadline);
        await using (var first = GrantWorkerTests.CreateHost(provider, test.Fixture.Scenario.Account.Journey.Lease.RuntimeConnection, test.Fixture.Clock))
        {
            await first.StartAsync(deadline.Token);
            await ObserveAsync(test, accepted.Job.Id, complete: false, deadline.Token);
            await first.StopAsync(deadline.Token);
        }
        var partial = await test.Grants.ReadAsync(accepted.Job.Id);
        Assert.Equal(GrantJobState.Processing, partial.State);
        Assert.InRange(partial.TokenCount, 1, RestartBatches - 1);
        await using (var restarted = GrantWorkerTests.CreateHost(provider, test.Fixture.Scenario.Account.Journey.Lease.RuntimeConnection, test.Fixture.Clock))
        {
            await restarted.StartAsync(deadline.Token);
            await ObserveAsync(test, accepted.Job.Id, complete: true, deadline.Token);
            await restarted.StopAsync(deadline.Token);
        }
        var complete = await test.Grants.ReadAsync(accepted.Job.Id);
        Assert.Equal(RestartBatches, complete.TokenCount);
        Assert.Equal(RestartBatches, complete.AuthorizationCount);
        Assert.Equal(partial.EffectiveApprovalId, complete.EffectiveApprovalId);
        Assert.Equal(receipt, ApplicationUpdateSnapshot.Hash(await test.ReceiptAsync()));
        Assert.True((await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id)).Policy.Retired);
    }

    /// <summary>Uses fresh explicit intent with exact current application and stopped-job versions.</summary>
    private static GrantIntent Continue(ApplicationRetirementTestContext test, string version, ApplicationGrantJob job) =>
        GrantIntent.Create(test.Id, version, Guid.NewGuid().ToString(GuidFormat), new(Reason, true, ApplicationVersion.Create(job.Id.ToString(GuidFormat), job.Revision)), job.Id)!;

    /// <summary>Only observes durable rows while the actual Worker owns all mutations, under one test deadline.</summary>
    private static async Task ObserveAsync(ApplicationRetirementTestContext test, string id, bool complete, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var job = await test.Grants.ReadAsync(id);
            Assert.NotEqual(GrantJobState.Stopped, job.State);
            if (complete ? job.State == GrantJobState.Succeeded : job.TokenCount > 0)
            {
                return;
            }
            await Task.Delay(ObservationInterval, token);
        }
    }
}
