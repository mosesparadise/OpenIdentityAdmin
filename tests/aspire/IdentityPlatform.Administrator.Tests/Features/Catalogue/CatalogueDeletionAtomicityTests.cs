using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Requires exact rollback after native deletion when terminal persistence is suppressed or substituted.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueDeletionAtomicityTests(BootstrapFixture fixture)
{
    /// <summary>Persisted uncommitted target absence cannot become success without exact original approval, terminal row and audit.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_AdmittedScopeDeletion_When_TerminalSaveIsSuppressedOrApprovalSubstituted_Then_AllNativeProductAndAuditChangesRollBack(string provider, bool substitute)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test, scope: true);
        var accepted = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        var original = CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Id));
        var fault = new CatalogueDeletionSaveFault(substitute);
        await using var worker = test.Peer(provider, fault);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CatalogueDeletionTest.ProcessAsync(test, accepted.Id, worker));
        Assert.True(fault.Observed);
        Assert.Equal(before, await CatalogueMutationEvidence.SnapshotAsync(test));
        Assert.Equal(original, CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, accepted.Id)));
        Assert.True(await CatalogueDeletionTest.ProcessAsync(test, accepted.Id));
        await CatalogueDeletionTest.RequireTerminalAsync(test, accepted.Id, CatalogueDeletionFailure.None);
    }
}
