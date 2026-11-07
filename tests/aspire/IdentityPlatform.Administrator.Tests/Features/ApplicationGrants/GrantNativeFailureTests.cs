using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Requires native-manager false, provider-denied audit and owned cancellation to preserve exact pre-attempt native state.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantNativeFailureTests(BootstrapFixture fixture)
{
    /// <summary>A real manager concurrency failure cannot count either family and must durably record one zero-progress stopped revision.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_NativeConcurrencyFailure_When_ManagerCannotRevoke_Then_AllNativeChangesRollBackAndJobStops(string provider)
    {
        var fault = new GrantNativeFault(concurrency: true);
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, fault);
        await test.SeedAsync(test.Id, 1, 1);
        var accepted = await test.AdmitAsync(test.Intent());
        var native = await test.NativeSnapshotAsync();
        fault.Armed = true;
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(fault.Observed);
        Assert.Equal(native, await test.NativeSnapshotAsync());
        var history = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        history.Require();
        Assert.Equal(GrantJobState.Stopped, history.Job.State);
        Assert.Equal(GrantJobFailure.Storage, history.Job.Failure);
        Assert.Equal(2, history.Job.Revision);
        Assert.Equal(0, history.Job.TokenCount);
        Assert.Equal(0, history.Job.AuthorizationCount);
    }

    /// <summary>When both progress and failure audits are denied by the real provider, no fictitious stopped state or native progress survives; restored storage can resume queued work.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ActualAuditInsertDenial_When_BatchAndFailureRecordingFail_Then_OriginalQueuedStateAndNativeEntriesSurvive(string provider)
    {
        var fault = new GrantNativeFault();
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, fault);
        await test.SeedAsync(test.Id, 1, 1);
        var accepted = await test.AdmitAsync(test.Intent());
        var native = await test.NativeSnapshotAsync();
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        fault.Armed = true;
        await IssuerAuditPermission.RunAsync(test.Fixture.Scenario.Account.Journey.Lease, async () =>
            await Assert.ThrowsAsync<DbUpdateException>(() => test.BatchAsync(accepted.Id)));
        Assert.True(fault.Observed);
        Assert.Equal(native, await test.NativeSnapshotAsync());
        await RequireUnchangedAsync(test, accepted.Id, before);
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.Equal(GrantJobState.Succeeded, (await test.ReadAsync(accepted.Id)).State);
    }

    /// <summary>Cancellation after an actual native save rolls back and drains the original scope before another fresh worker request can continue.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_NativeSave_When_OwnedBatchIsCancelled_Then_NoPartialProgressOrFalseStopIsRecorded(string provider)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var fault = new GrantNativeFault(cancellation: cancellation);
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, fault);
        await test.SeedAsync(test.Id, 1, 1);
        var accepted = await test.AdmitAsync(test.Intent());
        var native = await test.NativeSnapshotAsync();
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        fault.Armed = true;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GrantBatchOperation.ExecuteAsync(
            test.Fixture.Api.GetRequiredService<IServiceScopeFactory>(), Guid.Parse(accepted.Id), test.Fixture.Clock, cancellation.Token));
        Assert.True(fault.Observed);
        Assert.Equal(native, await test.NativeSnapshotAsync());
        await RequireUnchangedAsync(test, accepted.Id, before);
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.Equal(GrantJobState.Succeeded, (await test.ReadAsync(accepted.Id)).State);
    }

    /// <summary>Compares retained approval and every committed audit while requiring no new revision or invented progress.</summary>
    private static async Task RequireUnchangedAsync(GrantTestContext test, string id, GrantAuditWitness before)
    {
        var after = await GrantAuditWitness.ReadAsync(test, id);
        after.Require();
        after.Preserves(before);
        Assert.Equal(before.Job.Revision, after.Job.Revision);
        Assert.Equal(before.Job.State, after.Job.State);
        Assert.Equal(before.Job.EffectiveApprovalId, after.Job.EffectiveApprovalId);
        Assert.Equal(before.Job.AuthorizationCount, after.Job.AuthorizationCount);
        Assert.Equal(before.Job.TokenCount, after.Job.TokenCount);
    }
}
