using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Exercises delayed failure reconciliation against independently committed progress, explicit continuation and terminal success.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantLateFailureTests(BootstrapFixture fixture)
{
    private const string GuidFormat = "D";
    private const string Reason = "security";
    private const int MultipleBatches = 9;
    private const string ApprovalBypass = "grant-effective-approval-bypass";

    /// <summary>A delayed failure under the current approval stops later progress without erasing its committed native changes or audit receipts.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OldAttemptUnderCurrentApproval_When_LaterBatchCommitted_Then_StopPreservesCommittedProgress(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, MultipleBatches, MultipleBatches);
        var accepted = await test.AdmitAsync(test.Intent());
        var attempt = Attempt(await test.ReadAsync(accepted.Id));
        Assert.True(await test.BatchAsync(accepted.Id));
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        var native = await test.NativeSnapshotAsync();
        Assert.True(await RecordAsync(test, attempt));
        Assert.Equal(native, await test.NativeSnapshotAsync());
        var after = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        after.Require();
        after.Preserves(before);
        Assert.Equal(GrantJobState.Stopped, after.Job.State);
        Assert.Equal(GrantJobFailure.Storage, after.Job.Failure);
        Assert.Equal(8, after.Job.TokenCount);
        Assert.Equal(8, after.Job.AuthorizationCount);
        Assert.Equal(before.Job.Revision + 1, after.Job.Revision);
        Assert.False(await RecordAsync(test, attempt));
        Assert.Equal(native, await test.NativeSnapshotAsync());
    }

    /// <summary>A failure from a superseded approval cannot stop freshly approved continuation, even before any new native batch runs.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql", "CriticalDecision", "grant-effective-approval"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer", "CriticalDecision", "grant-effective-approval"])]
    public async Task Given_ExplicitContinuation_When_PreviousApprovalFailureArrives_Then_NewApprovalAndQueuedStateSurvive(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        var accepted = await test.AdmitAsync(test.Intent());
        var attempt = Attempt(await test.ReadAsync(accepted.Id));
        Assert.True(await RecordAsync(test, attempt));
        var stopped = await test.ReadAsync(accepted.Id);
        var intent = GrantIntent.Create(test.Id, test.Application.Version!, Guid.NewGuid().ToString(GuidFormat),
            new(Reason, true, ApplicationVersion.Create(accepted.Id, stopped.Revision)), stopped.Id)!;
        await test.AdmitAsync(intent);
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        var native = await test.NativeSnapshotAsync();
        Assert.NotEqual(attempt.ApprovalId, before.Job.EffectiveApprovalId);
        Assert.True(!await RecordAsync(test, attempt), ApprovalBypass);
        Assert.Equal(native, await test.NativeSnapshotAsync());
        var after = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        after.Require();
        after.Preserves(before);
        Assert.Equal(before.Job.Revision, after.Job.Revision);
        Assert.Equal(GrantJobState.Queued, after.Job.State);
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.Equal(GrantJobState.Succeeded, (await test.ReadAsync(accepted.Id)).State);
    }

    /// <summary>A late unsuccessful attempt never replaces terminal success or creates an extra audit revision.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_SuccessfulReconciliation_When_OldFailureArrives_Then_SuccessAndAuditRemainTerminal(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        var accepted = await test.AdmitAsync(test.Intent());
        var attempt = Attempt(await test.ReadAsync(accepted.Id));
        Assert.True(await test.BatchAsync(accepted.Id));
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        var native = await test.NativeSnapshotAsync();
        Assert.False(await RecordAsync(test, attempt));
        Assert.Equal(native, await test.NativeSnapshotAsync());
        var after = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        after.Require();
        after.Preserves(before);
        Assert.Equal(before.Job.Revision, after.Job.Revision);
        Assert.Equal(GrantJobState.Succeeded, after.Job.State);
    }

    /// <summary>Captures only the nonsecret failure identity a real attempt retains outside its disposed transaction.</summary>
    private static GrantAttempt Attempt(ApplicationGrantJob job) => new(job.Id) { ApprovalId = job.EffectiveApprovalId };

    /// <summary>Invokes the production fresh-owned reconciliation transaction without modifying stored state in a test callback.</summary>
    private static Task<bool> RecordAsync(GrantTestContext test, GrantAttempt attempt) => GrantFailure.RecordAsync(
        test.Fixture.Api.GetRequiredService<IServiceScopeFactory>(), attempt, test.Fixture.Clock, test.Token);
}
