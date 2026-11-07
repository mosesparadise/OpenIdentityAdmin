using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Exercises missing-user eligibility and the final persisted authority reread after actual batch progress has been saved.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantAuthorityBoundaryTests(BootstrapFixture fixture)
{
    private const string Grant = "grants.revoke";

    /// <summary>Deleting the native user while retaining its coordination metadata stops remaining work and preserves committed progress and approvals.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PartiallyProcessedJob_When_ActualActorIsDeleted_Then_AuthorityStopPreservesEarlierProgress(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, GrantAuthorityEvidence.Population, GrantAuthorityEvidence.Population);
        var control = await GrantAuthorityEvidence.ControlAsync(test);
        var peer = await GrantAuthorityEvidence.ReadAsync(test, control);
        var accepted = await test.AdmitAsync(test.Intent());
        Assert.True(await test.BatchAsync(accepted.Id));
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        before.Require();
        Assert.Equal(GrantAuthorityEvidence.Batch, before.Job.AuthorizationCount);
        Assert.Equal(GrantAuthorityEvidence.Batch, before.Job.TokenCount);
        var native = await GrantAuthorityEvidence.ReadAsync(test, test.Id);
        await DeleteAsync(test);
        Assert.True(await test.BatchAsync(accepted.Id));
        await GrantAuthorityEvidence.RequireStopAsync(test, accepted.Id, before);
        (await GrantAuthorityEvidence.ReadAsync(test, test.Id)).Require(native, 0);
        (await GrantAuthorityEvidence.ReadAsync(test, control)).Require(peer, 0);
    }

    /// <summary>Changes authority only after the native save, testing both a nonempty batch and empty-set success reconciliation.</summary>
    [Theory]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    public async Task Given_SavedBatch_When_CurrentAuthorityChangesBeforeFinalReread_Then_AllStagedEffectsRollBackAndWorkStops(string provider, bool permission, bool populated)
    {
        var change = new GrantAuthorityChange(permission);
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, change);
        await test.SeedAsync(test.Id, populated ? 1 : 0, populated ? 1 : 0);
        var control = await GrantAuthorityEvidence.ControlAsync(test);
        var peer = await GrantAuthorityEvidence.ReadAsync(test, control);
        var native = await GrantAuthorityEvidence.ReadAsync(test, test.Id);
        var authority = await AuthorityAsync(test);
        var accepted = await test.AdmitAsync(test.Intent());
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        change.Actor = test.Fixture.Identity.Subject;
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(change.Observed);
        Assert.Equal(populated ? GrantJobState.Processing : GrantJobState.Succeeded, change.SavedState);
        Assert.Equal(authority, await AuthorityAsync(test));
        await GrantAuthorityEvidence.RequireStopAsync(test, accepted.Id, before);
        (await GrantAuthorityEvidence.ReadAsync(test, test.Id)).Require(native, 0);
        (await GrantAuthorityEvidence.ReadAsync(test, control)).Require(peer, 0);
    }

    /// <summary>Uses the Identity manager to delete only the actual user, proving retained lock rows cannot substitute for account existence.</summary>
    private static async Task DeleteAsync(GrantTestContext test)
    {
        await using var scope = test.Fixture.Scope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var actor = test.Fixture.Identity.Subject;
        var manager = services.GetRequiredService<UserManager<PlatformUser>>();
        Assert.True((await manager.DeleteAsync((await manager.FindByIdAsync(actor))!)).Succeeded);
        Assert.False(await database.Users.AsNoTracking().AnyAsync(row => row.Id == actor, test.Token));
        Assert.True(await database.Set<AdministratorEnrollment>().AsNoTracking().AnyAsync(row => row.UserId == actor, test.Token));
        Assert.True(await database.Set<AdministratorAuthentication>().AsNoTracking().AnyAsync(row => row.UserId == actor, test.Token));
    }

    /// <summary>Requires exact restored permission rows and enabled account after the rejected transaction has drained.</summary>
    private static async Task<string> AuthorityAsync(GrantTestContext test)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await database.Users.AsNoTracking().Where(row => row.Id == test.Fixture.Identity.Subject).Select(row => row.IsEnabled).SingleAsync(test.Token));
        var claims = await database.RoleClaims.AsNoTracking().Where(row => row.ClaimValue == Grant).OrderBy(row => row.Id).ToArrayAsync(test.Token);
        Assert.NotEmpty(claims);
        return ApplicationUpdateSnapshot.Hash(claims);
    }
}
