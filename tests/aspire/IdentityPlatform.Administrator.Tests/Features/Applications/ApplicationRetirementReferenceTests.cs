using IdentityPlatform.Administrator.Tests.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Requires native scope references, clone provenance and relational ownership to survive terminal cleanup.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationRetirementReferenceTests(BootstrapFixture fixture)
{
    private const string Reason = "security";
    private const string GuidFormat = "D";
    private const int Ok = 200;
    private const int Conflict = 409;
    private const int Created = 201;

    /// <summary>Completed retirement cannot erase native assignment references or original clone provenance to make a catalogue deletion appear safe.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_AssignedScopeAndCloneHistory_When_RetirementFinishes_Then_AllReferencesRemainAndScopeDeletionIsRejected(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        var scope = (await ApplicationScopeEvidence.SeedAsync(test.Fixture))[0];
        var assigned = await test.Fixture.ExecuteAsync(ApplicationMutationCommand.AssignScopes(test.Id, test.Application.Version!, new([scope.Name]))!);
        Assert.Equal(Ok, assigned.Status);
        var clone = await test.Fixture.ExecuteAsync(ApplicationCloneAtomicityTests.Clone(test.Fixture, test.Id, assigned.Version!));
        Assert.Equal(Created, clone.Status);
        Assert.Equal(Ok, (await test.Fixture.ExecuteAsync(ApplicationMutationCommand.AssignScopes(clone.Application!.Id, clone.Version!, new([]))!)).Status);
        var clonedBefore = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, clone.Application!.Id);
        var permissions = await ApplicationScopeEvidence.ReadAsync(test.Fixture, test.Id);
        var accepted = await test.RetireAsync(test.Intent() with
        {
            Version = assigned.Version!
        });
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        var retained = await ApplicationScopeEvidence.ReadAsync(test.Fixture, test.Id);
        Assert.Equal(permissions.Permissions, retained.Permissions);
        var cloneAfter = await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, clone.Application.Id);
        Assert.Equal(clonedBefore.NativeDigest, cloneAfter.NativeDigest);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(clonedBefore.Policy), ApplicationUpdateSnapshot.Hash(cloneAfter.Policy));
        await using var inspection = test.Fixture.Scope();
        var database = inspection.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var provenance = await database.Set<ApplicationCloneAuditDetail>().AsNoTracking().SingleAsync(test.Token);
        Assert.Equal(test.Id, provenance.SourceApplicationId);
        Assert.Equal(assigned.Version, provenance.SourceVersion);
        var deletion = CatalogueDeleteIntent.Create(CatalogueResource.Scope, scope.Id, scope.Version, new(true, Guid.NewGuid().ToString(GuidFormat)))!;
        var beforeDeletion = await test.SnapshotAsync();
        var job = await CatalogueDeletionTest.AdmitAsync(test.Fixture, deletion);
        Assert.Equal(Conflict, job.Status);
        Assert.Null(job.Job);
        Assert.Equal(beforeDeletion, await test.SnapshotAsync());
        Assert.Empty(await database.Set<CatalogueDeletion>().AsNoTracking().ToArrayAsync(test.Token));
    }

    /// <summary>The physical terminal check and restrictive receipt parent survive direct invalid writes from an independent context.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PersistedRetirement_When_EnabledStateOrReceiptParentIsMutated_Then_DatabaseRejectsAndCompleteStateRemains(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        await test.RetireAsync(test.Intent());
        var before = await test.SnapshotAsync();
        await using (var scope = test.Fixture.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var policy = await database.Set<ApplicationPolicy>().SingleAsync(test.Token);
            policy.Enabled = true;
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(test.Token));
        }
        Assert.Equal(before, await test.SnapshotAsync());
        await using (var scope = test.Fixture.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            database.Remove(await database.Set<ApplicationPolicy>().SingleAsync(test.Token));
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(test.Token));
        }
        Assert.Equal(before, await test.SnapshotAsync());
    }

    /// <summary>The independent Retired token catches a stale EF writer even if a concurrent direct terminal transition deliberately leaves Revision unchanged.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_StalePolicySnapshot_When_RetiredChangesWithoutRevision_Then_IndependentConcurrencyTokenRejectsWriter(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        Assert.Equal(Ok, (await test.Fixture.ExecuteAsync(ApplicationMutationCommand.Lifecycle(test.Id, test.Application.Version!, new(Reason, true), false)!)).Status);
        await using var staleScope = test.Fixture.Scope();
        var stale = staleScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var policy = await stale.Set<ApplicationPolicy>().SingleAsync(test.Token);
        await using (var scope = test.Fixture.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.Equal(1, await database.Set<ApplicationPolicy>().Where(row => row.ApplicationId == test.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Retired, true), test.Token));
        }
        var before = await test.SnapshotAsync();
        policy.Revision++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(test.Token));
        Assert.Equal(before, await test.SnapshotAsync());
    }
}
