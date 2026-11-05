using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Requires missing lock metadata and a factor outside the current generation to deny native work without rewriting immutable approvals.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantMetadataTests(BootstrapFixture fixture)
{
    private const string MissingLock = "missing-lock";
    private const string WrongGeneration = "wrong-generation";

    /// <summary>Actual persisted metadata loss stops via fresh governance-only failure ownership; no token or authorization transition is committed.</summary>
    [Theory]
    [InlineData("PostgreSql", MissingLock, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", WrongGeneration, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", MissingLock, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", WrongGeneration, Traits = ["Provider", "SqlServer"])]
    public async Task Given_AcceptedJob_When_CurrentActorMetadataCannotAuthorize_Then_NativeStateAndPriorApprovalSurvive(string provider, string change)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, 1, 1);
        var accepted = await test.AdmitAsync(test.Intent());
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        await ChangeAsync(test, change);
        var native = await test.NativeSnapshotAsync();
        Assert.True(await test.BatchAsync(accepted.Id));
        var after = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        after.Require();
        after.Preserves(before);
        Assert.Equal(GrantJobState.Stopped, after.Job.State);
        Assert.Equal(change == MissingLock ? GrantJobFailure.Storage : GrantJobFailure.Authority, after.Job.Failure);
        Assert.Equal(0, after.Job.AuthorizationCount);
        Assert.Equal(0, after.Job.TokenCount);
        Assert.Equal(native, await test.NativeSnapshotAsync());
        Assert.False(await test.BatchAsync(accepted.Id));
    }

    /// <summary>Keeps allocation consistent while leaving the existing factor at its old generation, or deletes the actual actor-lock row.</summary>
    private static async Task ChangeAsync(GrantTestContext test, string change)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var actor = test.Fixture.Identity.Subject;
        if (change == MissingLock)
        {
            Assert.Equal(1, await database.Set<AdministratorAuthentication>().Where(row => row.UserId == actor).ExecuteDeleteAsync(test.Token));
            return;
        }
        Assert.Equal(WrongGeneration, change);
        var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(row => row.UserId == actor, test.Token);
        var authentication = await database.Set<AdministratorAuthentication>().SingleAsync(row => row.UserId == actor, test.Token);
        enrollment.FactorGeneration++;
        authentication.LastAllocatedFactorGeneration = enrollment.FactorGeneration;
        await database.SaveChangesAsync(test.Token);
    }
}
