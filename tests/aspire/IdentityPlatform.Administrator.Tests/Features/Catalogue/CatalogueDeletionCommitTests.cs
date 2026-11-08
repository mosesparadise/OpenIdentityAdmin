using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Distinguishes rollback from actual committed deletion when acknowledgement is lost, without retrying a mutation blindly.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueDeletionCommitTests(BootstrapFixture fixture)
{
    /// <summary>Actual transaction interception must leave either the complete original state or one fully audited terminal state.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_QueuedScopeDeletion_When_CommitFailsBeforeOrAfterDurability_Then_FreshObservationDeterminesExactOutcome(string provider, bool after)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test, scope: true);
        var accepted = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        var original = CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Id));
        var fault = new GrantCommitFault(after) { Armed = true };
        await using var peer = test.Peer(provider, fault);
        await Assert.ThrowsAsync<IOException>(() => CatalogueDeletionTest.ProcessAsync(test, accepted.Id, peer));
        Assert.True(fault.Observed);
        if (!after)
        {
            Assert.Equal(before, await CatalogueMutationEvidence.SnapshotAsync(test));
            Assert.Equal(original, CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Id)));
            Assert.True(await CatalogueDeletionTest.ProcessAsync(test, accepted.Id));
        }
        await CatalogueDeletionTest.RequireTerminalAsync(test, accepted.Id, CatalogueDeletionFailure.None);
    }

    /// <summary>A lost acknowledgement never creates a second approval or renews the original deadline when the caller recovers the exact key.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_AdmissionCommit_When_AcknowledgementIsLost_Then_ExactReplayReturnsOneUnchangedDurableApproval(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test);
        var fault = new GrantCommitFault(after: true) { Armed = true };
        await using var peer = test.Peer(provider, fault);
        await Assert.ThrowsAsync<IOException>(() => CatalogueDeletionTest.AdmitAsync(test, intent, peer));
        Assert.True(fault.Observed);
        await using var observer = test.Scope();
        var original = await observer.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<CatalogueDeletion>().AsNoTracking().SingleAsync(test.Token);
        var snapshot = CatalogueDeletionSnapshot.Capture(original);
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        var recovered = await CatalogueDeletionTest.AdmitAsync(test, intent);
        Assert.Equal(202, recovered.Status);
        Assert.Equal(original.Id, recovered.Job!.Id);
        Assert.Equal(snapshot, CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, original.Id)));
        Assert.Equal(before, await CatalogueMutationEvidence.SnapshotAsync(test));
    }
}
