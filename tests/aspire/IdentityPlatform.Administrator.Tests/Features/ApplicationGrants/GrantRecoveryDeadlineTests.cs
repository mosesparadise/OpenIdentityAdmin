using System.Diagnostics;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Requires real elapsed owned deadlines independently of frozen UTC, caller cancellation and other narrower lifetime guards.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantRecoveryDeadlineTests(BootstrapFixture fixture)
{
    private const string Elapsed = "grant-{0}-deadline-elapsed-ms={1:F3}";
    private const string Transaction = "transaction";
    private const string Request = "request";

    /// <summary>Expiry after a real native save rolls back all progress and records one fresh zero-delta storage stop under the original approval.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_NativeSave_When_TransactionDeadlineElapsesWithLiveRequest_Then_OriginalChangesRollBackAndFreshStopCommits(string provider)
    {
        var probe = new GrantTransactionDeadline();
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, probe);
        await test.SeedAsync(test.Id, 1, 1);
        _ = await GrantAuthorityEvidence.ControlAsync(test);
        var accepted = await test.AdmitAsync(test.Intent());
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        var native = await test.NativeSnapshotAsync();
        var configuration = await GrantAuthorityEvidence.ReadAsync(test, test.Id);
        var utc = test.Fixture.Clock.Now;
        probe.Armed = true;
        var started = Stopwatch.GetTimestamp();
        Assert.True(await test.BatchAsync(accepted.Id));
        TestContext.Current.TestOutputHelper?.WriteLine(Elapsed, Transaction, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        probe.Require();
        Assert.Equal(utc, test.Fixture.Clock.Now);
        var after = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        after.Require();
        after.Preserves(before);
        Assert.Equal(GrantJobState.Stopped, after.Job.State);
        Assert.Equal(GrantJobFailure.Storage, after.Job.Failure);
        Assert.Equal(before.Job.Revision + 1, after.Job.Revision);
        Assert.Single(after.Approvals);
        Assert.False(await test.BatchAsync(accepted.Id));
        Assert.Equal(native, await test.NativeSnapshotAsync());
        (await GrantAuthorityEvidence.ReadAsync(test, test.Id)).Require(configuration, 0);
    }

    /// <summary>Activation outliving the original request cannot create a transaction, progress or false stop; an independent fresh request can still complete queued work.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealNativeContextActivation_When_OriginalRequestDeadlineElapses_Then_NoTransactionOrMutationOccursAndFreshWorkCanResume(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        var probe = new GrantRequestDeadline();
        await using var peer = test.Fixture.Peer(provider, configure: probe.Configure);
        await test.SeedAsync(test.Id, 1, 1);
        _ = await GrantAuthorityEvidence.ControlAsync(test);
        var accepted = await test.AdmitAsync(test.Intent());
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        var native = await test.NativeSnapshotAsync();
        var configuration = await GrantAuthorityEvidence.ReadAsync(test, test.Id);
        var utc = test.Fixture.Clock.Now;
        probe.Armed = true;
        var started = Stopwatch.GetTimestamp();
        Assert.False(await GrantBatchOperation.ExecuteAsync(peer.GetRequiredService<IServiceScopeFactory>(), Guid.Parse(accepted.Id), test.Fixture.Clock, test.Token));
        TestContext.Current.TestOutputHelper?.WriteLine(Elapsed, Request, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        probe.Require();
        Assert.Equal(utc, test.Fixture.Clock.Now);
        var after = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        after.Require();
        after.Preserves(before);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Job), ApplicationUpdateSnapshot.Hash(after.Job));
        Assert.Single(after.Approvals);
        Assert.Single(after.Details);
        Assert.Equal(native, await test.NativeSnapshotAsync());
        (await GrantAuthorityEvidence.ReadAsync(test, test.Id)).Require(configuration, 0);
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(await test.BatchAsync(accepted.Id));
        var completed = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        completed.Require();
        Assert.Equal(GrantJobState.Succeeded, completed.Job.State);
        (await GrantAuthorityEvidence.ReadAsync(test, test.Id)).Require(configuration, 1);
    }
}
