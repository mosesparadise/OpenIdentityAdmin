using IdentityPlatform.Administrator.Tests.Features.Catalogue;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Requires exact persisted rollback for ineligible catalogue selections and current application authority failures.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationScopeDenialTests(BootstrapFixture fixture)
{
    private const string Write = "applications.write";
    private const string CatalogueWrite = "scopes.write";
    private const string Missing = "missing.read";
    private const string Alias = "ORDERS.read";
    private const int Freshness = 300;

    private const int MissingPolicy = 2;
    private const int DisabledScope = 3;
    private const int DisabledAudience = 4;

    /// <summary>Missing, case-aliased, unmanaged or disabled scope/audience state cannot become a native client permission.</summary>
    [Theory]
    [InlineData("PostgreSql", 0, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", 1, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", 2, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", 3, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", 4, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", 0, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", 1, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", 2, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", 3, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", 4, Traits = ["Provider", "SqlServer"])]
    public async Task Given_IneligibleScopePolicy_When_AssignmentRequested_Then_ApplicationCatalogueAndAuditAreUnchanged(string provider, int denial)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var scopes = await ApplicationScopeEvidence.SeedAsync(test);
        var current = await test.ExecuteAsync(test.Create());
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var id = scopes[1].Id;
        if (denial == MissingPolicy)
        {
            await database.Set<ScopePolicy>().Where(row => row.ScopeId == id).ExecuteDeleteAsync(test.Token);
        }
        if (denial == DisabledScope)
        {
            await database.Set<ScopePolicy>().Where(row => row.ScopeId == id).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Enabled, false), test.Token);
        }
        if (denial == DisabledAudience)
        {
            await database.Set<ApiAudience>().ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Enabled, false), test.Token);
        }
        var name = denial == 0 ? Missing : denial == 1 ? Alias : ApplicationScopeEvidence.Orders;
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, current.Application!.Id);
        var catalogue = await CatalogueMutationEvidence.SnapshotAsync(test);
        var result = await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(current.Application.Id, current.Version!, new([name]))!);
        Assert.Equal(409, result.Status);
        Assert.Null(result.Scopes);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, current.Application.Id)).Digest);
        Assert.Equal(catalogue, await CatalogueMutationEvidence.SnapshotAsync(test));
    }

    /// <summary>Application-write permission independently owns assignment; a catalogue writer grant is neither required nor sufficient, and MFA equality fails closed.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CurrentAssignmentAuthority_When_GrantRemovedOrMfaExpires_Then_NoDeniedMutationCommits(string provider, bool expire)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await ApplicationScopeEvidence.SeedAsync(test);
        var current = await test.ExecuteAsync(test.Create());
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == CatalogueWrite).ExecuteDeleteAsync(test.Token) > 0);
        current = await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(current.Application!.Id, current.Version!, new([ApplicationScopeEvidence.Email]))!);
        Assert.Equal(200, current.Status);
        if (expire)
        {
            var verified = await database.Set<AdministratorSession>().Select(row => row.FactorVerifiedAt).SingleAsync(test.Token);
            test.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(verified + Freshness);
        }
        else
        {
            Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == Write).ExecuteDeleteAsync(test.Token) > 0);
        }
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, current.Application!.Id);
        Assert.Equal(403, (await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(current.Application.Id, current.Version!, new([]))!)).Status);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, current.Application.Id)).Digest);
    }
}
