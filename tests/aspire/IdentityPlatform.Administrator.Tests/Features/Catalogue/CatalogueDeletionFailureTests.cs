using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Proves real runtime audit denial and request cancellation after native deletion roll back all effects and permit fresh recovery.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueDeletionFailureTests(BootstrapFixture fixture)
{
    /// <summary>Both failures occur after actual uncommitted scope absence, yet preserve the complete durable target, approval and audit snapshots.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_DeletedUncommittedScope_When_AuditWriteIsDeniedOrRequestCancelled_Then_ExactOriginalStateRollsBackBeforeRecovery(string provider, bool cancelled)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test, scope: true);
        var accepted = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        var original = CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Id));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(test.Token);
        var witness = new CatalogueDeletionTerminalWitness(cancelled ? cancellation.Cancel : null);
        await using var peer = test.Peer(provider, witness);
        if (cancelled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CatalogueDeletionProcessor.ExecuteAsync(peer.GetRequiredService<IServiceScopeFactory>(), accepted.Id, test.Clock, cancellation.Token));
        }
        else
        {
            await IssuerAuditPermission.RunAsync(test.Scenario.Account.Journey.Lease, async () =>
                await Assert.ThrowsAsync<DbUpdateException>(() => CatalogueDeletionTest.ProcessAsync(test, accepted.Id, peer)));
        }
        Assert.True(witness.Observed);
        Assert.Equal(before, await CatalogueMutationEvidence.SnapshotAsync(test));
        Assert.Equal(original, CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Id)));
        Assert.True(await CatalogueDeletionTest.ProcessAsync(test, accepted.Id));
        await CatalogueDeletionTest.RequireTerminalAsync(test, accepted.Id, CatalogueDeletionFailure.None);
    }
}
