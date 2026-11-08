using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Rechecks original approval, current security bindings and target/reference state after durable admission on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueDeletionDenialTests(BootstrapFixture fixture)
{
    private const string Expired = "expired";
    private const string Rollback = "rollback";
    private const string Permission = "permission";
    private const string Disabled = "disabled";
    private const string Generation = "generation";
    private const string Stale = "stale";
    private const string Referenced = "referenced";
    private const string AudiencePermission = "audiences.write";
    private const string ReferenceName = "unmanaged-reference";
    private const int TargetSnapshotCount = 3;
    private const int BeforeAdmission = -1;

    /// <summary>Every denial leaves the target intact and commits only one finite terminal rejection linked to original admission.</summary>
    [Theory]
    [InlineData("PostgreSql", Expired, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Rollback, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Permission, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Disabled, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Generation, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Stale, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Referenced, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Expired, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Rollback, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Permission, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Disabled, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Generation, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Stale, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Referenced, Traits = ["Provider", "SqlServer"])]
    public async Task Given_AdmittedDeletion_When_OriginalAuthorityOrTargetChanges_Then_JobRejectsWithoutDeleting(string provider, string change)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test);
        var admitted = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        var original = CatalogueDeletionSnapshot.Capture(await CatalogueDeletionTest.ReadAsync(test, admitted.Id));
        var failure = await ChangeAsync(test, original, change);
        var before = await CatalogueMutationEvidence.SnapshotAsync(test);
        Assert.True(await CatalogueDeletionTest.ProcessAsync(test, admitted.Id));
        Assert.Equal(before.Take(TargetSnapshotCount), (await CatalogueMutationEvidence.SnapshotAsync(test)).Take(TargetSnapshotCount));
        await CatalogueDeletionTest.RequireTerminalAsync(test, admitted.Id, failure);
        var terminal = await CatalogueDeletionTest.ReadAsync(test, admitted.Id);
        Assert.Equal(original.ApprovalUntil, terminal.ApprovalUntil);
        Assert.True(terminal.ObservedAt >= original.ObservedAt);
    }

    /// <summary>Changes real persisted state through an independent setup context without replacing production authority or reference logic.</summary>
    private static async Task<CatalogueDeletionFailure> ChangeAsync(ApplicationMutationFixture test, CatalogueDeletionSnapshot job, string change)
    {
        if (change is Expired or Rollback)
        {
            test.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(change == Expired ? job.ApprovalUntil : job.CreatedAt + BeforeAdmission);
            return CatalogueDeletionFailure.Expired;
        }
        await using var setup = test.Scope();
        var database = setup.ServiceProvider.GetRequiredService<PlatformDbContext>();
        switch (change)
        {
            case Permission:
                Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == AudiencePermission).ExecuteDeleteAsync(test.Token) > 0);
                return CatalogueDeletionFailure.Authority;
            case Disabled:
                Assert.Equal(1, await database.Users.Where(row => row.Id == job.ActorId).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.IsEnabled, false), test.Token));
                return CatalogueDeletionFailure.Authority;
            case Generation:
                Assert.Equal(1, await database.Set<AdministratorAuthentication>().Where(row => row.UserId == job.ActorId).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Generation, row => row.Generation + 1), test.Token));
                return CatalogueDeletionFailure.Authority;
            case Stale:
                Assert.Equal(1, await database.Set<ApiAudience>().Where(row => row.Id == job.TargetId).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Revision, row => row.Revision + 1), test.Token));
                return CatalogueDeletionFailure.Stale;
            case Referenced:
                var audience = await database.Set<ApiAudience>().AsNoTracking().SingleAsync(row => row.Id == job.TargetId, test.Token);
                var descriptor = new OpenIddictScopeDescriptor { Name = ReferenceName };
                descriptor.Resources.Add(audience.Resource);
                await setup.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>().CreateAsync(descriptor, test.Token);
                return CatalogueDeletionFailure.Referenced;
            default:
                throw new InvalidOperationException();
        }
    }
}
