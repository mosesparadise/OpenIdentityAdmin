using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Proves actual hosted cancellation drains a blocked provider operation and restart uses the unchanged original approval.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueDeletionRestartTests(BootstrapFixture fixture)
{
    private const int Contender = 1;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ObservationInterval = TimeSpan.FromMilliseconds(25);

    /// <summary>Shutdown joins real blocked work before restart; expired approvals reject and current approvals complete without readmission.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_BlockedHostedDeletion_When_StopDrainsAndHostRestarts_Then_OnlyOriginalApprovalControlsCompletion(string provider, bool expired)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test, scope: true);
        var accepted = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        var original = CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Id));
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        var lease = test.Scenario.Account.Journey.Lease;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(test.Token);
        deadline.CancelAfter(Deadline);
        await using (var held = await GovernanceLock.AcquireAsync(lease))
        await using (var first = GrantWorkerTests.CreateHost(provider, lease.RuntimeConnection, test.Clock))
        {
            await first.StartAsync(deadline.Token);
            await held.ObserveOwnedAsync(lease, Contender, deadline.Token);
            await first.StopAsync(deadline.Token);
            Assert.Equal(original, CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Id)));
            Assert.Equal(before, await CatalogueMutationEvidence.SnapshotAsync(test));
        }
        if (expired)
        {
            test.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(original.ApprovalUntil);
        }
        await using var restarted = GrantWorkerTests.CreateHost(provider, lease.RuntimeConnection, test.Clock);
        await restarted.StartAsync(deadline.Token);
        await ObserveAsync(test, accepted.Id, deadline.Token);
        await restarted.StopAsync(deadline.Token);
        await CatalogueDeletionTest.RequireTerminalAsync(test, accepted.Id, expired ? CatalogueDeletionFailure.Expired : CatalogueDeletionFailure.None);
        var terminal = await CatalogueDeletionTest.ReadAsync(test, accepted.Id);
        Assert.Equal(original.ApprovalUntil, terminal.ApprovalUntil);
        Assert.Equal(original.AdmissionAuditId, terminal.AdmissionAuditId);
    }

    /// <summary>Observes independently persisted terminal truth without admitting, processing or retrying a job from the test.</summary>
    internal static async Task ObserveAsync(ApplicationMutationFixture test, Guid id, CancellationToken token)
    {
        while ((await CatalogueDeletionTest.ReadAsync(test, id)).State == CatalogueDeletionState.Queued)
        {
            await Task.Delay(ObservationInterval, token);
        }
    }
}
