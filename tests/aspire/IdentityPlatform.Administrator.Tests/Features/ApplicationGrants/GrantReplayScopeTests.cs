using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Exercises actor, application and action key namespaces through genuine current authority and durable histories on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantReplayScopeTests(BootstrapFixture fixture)
{
    private const int Conflict = 409;
    private const int Created = 201;
    private const int Ok = 200;
    private const string Peer = "grant-key-scope-peer";
    private const string Reason = "security";
    private const string GuidFormat = "D";

    /// <summary>A different eligible actor cannot recover another actor's acceptance, even after completion removes the incomplete-job guard.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RetainedActorKey_When_AnotherAdministratorUsesIdenticalIntent_Then_OnlyItsOwnApprovalCanBeRecovered(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        var other = await GrantReplayEvidence.OtherActorAsync(test);
        await test.SeedAsync(test.Id, 1, 1);
        var intent = test.Intent();
        var first = await test.AdmitAsync(intent);
        var before = await GrantReplayEvidence.SnapshotAsync(test);
        var denied = await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.AdmitOnAsync(test.Fixture.Api, intent, other));
        Assert.Equal(Conflict, denied.Status);
        Assert.Equal(before, await GrantReplayEvidence.SnapshotAsync(test));
        Assert.True(await test.BatchAsync(first.Id));
        Assert.True(await test.BatchAsync(first.Id));
        var complete = Assert.Single(await GrantReplayEvidence.HistoriesAsync(test, first.Id));
        Assert.Equal(GrantJobState.Succeeded, complete.Job.State);
        var native = await test.NativeSnapshotAsync();
        var configuration = await GrantReplayEvidence.ConfigurationAsync(test, test.Id);
        var second = await test.AdmitOnAsync(test.Fixture.Api, intent, other);
        Assert.NotEqual(first.Id, second.Id);
        var histories = await GrantReplayEvidence.HistoriesAsync(test, first.Id, second.Id);
        GrantReplayEvidence.Unchanged(complete, histories[0]);
        Assert.Equal(GrantJobState.Queued, histories[1].Job.State);
        RequireApproval(histories[0], test.Fixture.Identity.Subject, intent.RequestKey, GrantApprovalAction.Submit);
        RequireApproval(histories[1], other.Subject, intent.RequestKey, GrantApprovalAction.Submit);
        Assert.NotEqual(histories[0].Approvals[0].ScopedKeyHash, histories[1].Approvals[0].ScopedKeyHash);
        before = await GrantReplayEvidence.SnapshotAsync(test);
        Assert.Equal(first.Id, (await test.AdmitAsync(intent)).Id);
        Assert.Equal(second.Id, (await test.AdmitOnAsync(test.Fixture.Api, intent, other)).Id);
        Assert.Equal(before, await GrantReplayEvidence.SnapshotAsync(test));
        Assert.Equal(native, await test.NativeSnapshotAsync());
        Assert.Equal(configuration, await GrantReplayEvidence.ConfigurationAsync(test, test.Id));
    }

    /// <summary>The same actor/key accepts and recovers a different disabled target while the original target's job is still incomplete.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RetainedTargetKey_When_AnotherDisabledApplicationUsesThatKey_Then_EachTargetRetainsItsOwnJobAndHistory(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        var peer = await test.Fixture.ExecuteAsync(test.Fixture.Create(Peer));
        Assert.Equal(Created, peer.Status);
        peer = await test.Fixture.ExecuteAsync(ApplicationMutationCommand.Lifecycle(peer.Application!.Id, peer.Version!, new(Reason, true), false)!);
        Assert.Equal(Ok, peer.Status);
        await test.SeedAsync(test.Id, 1, 1);
        await test.SeedAsync(peer.Application!.Id, 1, 1);
        var intent = test.Intent();
        var first = await test.AdmitAsync(intent);
        var original = Assert.Single(await GrantReplayEvidence.HistoriesAsync(test, first.Id));
        var peerIntent = GrantIntent.Create(peer.Application.Id, peer.Version!, intent.RequestKey.ToString(GuidFormat), new(Reason, true))!;
        var native = await test.NativeSnapshotAsync();
        var configuration = await GrantReplayEvidence.ConfigurationAsync(test, test.Id);
        var peerConfiguration = await GrantReplayEvidence.ConfigurationAsync(test, peer.Application.Id);
        var second = await test.AdmitAsync(peerIntent);
        Assert.NotEqual(first.Id, second.Id);
        var histories = await GrantReplayEvidence.HistoriesAsync(test, first.Id, second.Id);
        GrantReplayEvidence.Unchanged(original, histories[0]);
        Assert.All(histories, history => Assert.Equal(GrantJobState.Queued, history.Job.State));
        Assert.Equal(peer.Application.Id, histories[1].Job.ApplicationId);
        RequireApproval(histories[1], test.Fixture.Identity.Subject, intent.RequestKey, GrantApprovalAction.Submit);
        Assert.NotEqual(histories[0].Approvals[0].ScopedKeyHash, histories[1].Approvals[0].ScopedKeyHash);
        var before = await GrantReplayEvidence.SnapshotAsync(test);
        Assert.Equal(first.Id, (await test.AdmitAsync(intent)).Id);
        Assert.Equal(second.Id, (await test.AdmitAsync(peerIntent)).Id);
        Assert.Equal(before, await GrantReplayEvidence.SnapshotAsync(test));
        Assert.Equal(native, await test.NativeSnapshotAsync());
        Assert.Equal(configuration, await GrantReplayEvidence.ConfigurationAsync(test, test.Id));
        Assert.Equal(peerConfiguration, await GrantReplayEvidence.ConfigurationAsync(test, peer.Application.Id));
    }

    /// <summary>Submission and continuation are separate action namespaces, while replay within the continuation namespace cannot add another approval.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RetainedSubmissionKey_When_FreshContinuationUsesTheSameUuid_Then_NewActionApprovalAndExactReplayRemainDistinct(string provider)
    {
        var fault = new GrantNativeFault(concurrency: true);
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, fault);
        await test.SeedAsync(test.Id, 1, 1);
        var intent = test.Intent();
        var accepted = await test.AdmitAsync(intent);
        var native = await test.NativeSnapshotAsync();
        var configuration = await GrantReplayEvidence.ConfigurationAsync(test, test.Id);
        fault.Armed = true;
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(fault.Observed);
        var stopped = Assert.Single(await GrantReplayEvidence.HistoriesAsync(test, accepted.Id));
        Assert.Equal(GrantJobState.Stopped, stopped.Job.State);
        var continuation = GrantIntent.Create(test.Id, test.Application.Version!, intent.RequestKey.ToString(GuidFormat),
            new(Reason, true, ApplicationVersion.Create(accepted.Id, stopped.Job.Revision)), stopped.Job.Id)!;
        Assert.Equal(accepted.Id, (await test.AdmitAsync(continuation)).Id);
        var resumed = Assert.Single(await GrantReplayEvidence.HistoriesAsync(test, accepted.Id));
        resumed.Preserves(stopped);
        Assert.Equal(GrantJobState.Queued, resumed.Job.State);
        Assert.Equal(2, resumed.Approvals.Length);
        var approval = Assert.Single(resumed.Approvals, row => row.Id == resumed.Job.EffectiveApprovalId);
        Assert.Equal(test.Fixture.Identity.Subject, approval.ActorId);
        Assert.Equal(GrantApprovalAction.Continue, approval.Action);
        Assert.Equal(intent.RequestKey, approval.RequestKey);
        Assert.NotEqual(stopped.Approvals[0].ScopedKeyHash, approval.ScopedKeyHash);
        var before = await GrantReplayEvidence.SnapshotAsync(test);
        Assert.Equal(accepted.Id, (await test.AdmitAsync(continuation)).Id);
        Assert.Equal(before, await GrantReplayEvidence.SnapshotAsync(test));
        Assert.Equal(native, await test.NativeSnapshotAsync());
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(await test.BatchAsync(accepted.Id));
        var completed = Assert.Single(await GrantReplayEvidence.HistoriesAsync(test, accepted.Id));
        completed.Preserves(resumed);
        Assert.Equal(GrantJobState.Succeeded, completed.Job.State);
        Assert.Equal(2, completed.Approvals.Length);
        Assert.Equal(configuration, await GrantReplayEvidence.ConfigurationAsync(test, test.Id));
    }

    /// <summary>Checks independent persisted actor/action/key/target bindings rather than reusing the production digest formula as the oracle.</summary>
    private static void RequireApproval(GrantAuditWitness history, string actor, Guid key, GrantApprovalAction action)
    {
        var approval = Assert.Single(history.Approvals);
        Assert.Equal(actor, approval.ActorId);
        Assert.Equal(key, approval.RequestKey);
        Assert.Equal(action, approval.Action);
        Assert.Equal(history.Job.ApplicationId, approval.ApplicationId);
    }
}
