using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Requires current application authority and supported source/catalogue state without borrowing unrelated grants.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationCloneDenialTests(BootstrapFixture fixture)
{
    private const int MissingStatus = 404;
    private const int ForbiddenStatus = 403;
    private const int ConflictStatus = 409;
    private const string Write = "applications.write";
    private const string CatalogueWrite = "scopes.write";
    private const string Credentials = "applications.credentials";
    private const string DeniedClient = "denied-clone";
    private const string Missing = "missing-source";
    private const string Unmanaged = "unmanaged-source";
    private const string MissingPolicy = "missing-policy";
    private const string DisabledScope = "disabled-scope";
    private const string DisabledAudience = "disabled-audience";

    /// <summary>Cloning assigned scopes needs application write and fresh MFA; unrelated catalogue/credential grants neither confer nor restrict it.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CurrentCloneAuthority_When_UnrelatedThenWriteGrantsAreRemoved_Then_OnlyWriteRemovalDenies(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await ApplicationScopeEvidence.SeedAsync(test);
        var source = await test.ExecuteAsync(test.Create());
        source = await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(source.Application!.Id, source.Version!, new([ApplicationScopeEvidence.Email]))!);
        Assert.Equal(200, source.Status);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        foreach (var permission in new[] { CatalogueWrite, Credentials })
        {
            Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == permission).ExecuteDeleteAsync(test.Token) > 0);
        }
        Assert.Equal(201, (await test.ExecuteAsync(ApplicationCloneAtomicityTests.Clone(test, source.Application!.Id, source.Version!))).Status);
        Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == Write).ExecuteDeleteAsync(test.Token) > 0);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, source.Application.Id);
        var denied = await test.ExecuteAsync(ApplicationCloneAtomicityTests.Clone(test, source.Application.Id, source.Version!, DeniedClient));
        Assert.Equal(403, denied.Status);
        Assert.Null(denied.Credential);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, source.Application.Id)).Digest);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().FindByClientIdAsync(DeniedClient, test.Token));
    }

    /// <summary>Missing, persisted protected and unmanaged identities plus reserved destinations cannot become clone targets or sources.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_UnsupportedCloneBoundary_When_Submitted_Then_NoCredentialOrAuditIsCreated(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var source = await test.ExecuteAsync(test.Create());
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var protectedId = await database.Set<AdministratorProtocolProfile>().Select(row => row.ApplicationId).SingleAsync(test.Token);
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var native = await manager.CreateAsync(new OpenIddictApplicationDescriptor { ClientId = Unmanaged, ClientType = OpenIddictConstants.ClientTypes.Public }, test.Token);
        var unmanagedId = (await manager.GetIdAsync(native, test.Token))!;
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, source.Application!.Id);
        foreach (var (id, status) in new[] { (Missing, MissingStatus), (protectedId, ForbiddenStatus), (unmanagedId, ConflictStatus) })
        {
            var result = await test.ExecuteAsync(ApplicationCloneAtomicityTests.Clone(test, id, ApplicationVersion.Create(id, 1)));
            Assert.Equal(status, result.Status);
            Assert.Null(result.Credential);
            Assert.Null(result.Version);
        }
        var reserved = await test.ExecuteAsync(ApplicationCloneAtomicityTests.Clone(test, source.Application.Id, source.Version!, AdministratorProtocolIdentity.ClientId));
        Assert.Equal(403, reserved.Status);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, source.Application.Id)).Digest);
        Assert.Empty(await database.Set<ApplicationCloneAuditDetail>().AsNoTracking().ToArrayAsync(test.Token));
    }

    /// <summary>A previously valid source assignment cannot be copied after its scope or audience loses current eligibility.</summary>
    [Theory]
    [InlineData("PostgreSql", MissingPolicy, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", DisabledScope, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", DisabledAudience, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", MissingPolicy, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", DisabledScope, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", DisabledAudience, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PreviouslyEligibleAssignment_When_CurrentCatalogueIsIneligible_Then_CloneLeavesSourceAndHistoryUnchanged(string provider, string denial)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var selected = (await ApplicationScopeEvidence.SeedAsync(test))[1];
        var source = await test.ExecuteAsync(test.Create());
        source = await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(source.Application!.Id, source.Version!, new([selected.Name]))!);
        Assert.Equal(200, source.Status);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        if (denial == MissingPolicy)
        {
            await database.Set<ScopePolicy>().Where(row => row.ScopeId == selected.Id).ExecuteDeleteAsync(test.Token);
        }
        else if (denial == DisabledScope)
        {
            await database.Set<ScopePolicy>().Where(row => row.ScopeId == selected.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.Enabled, false), test.Token);
        }
        else
        {
            await database.Set<ApiAudience>().ExecuteUpdateAsync(set => set.SetProperty(row => row.Enabled, false), test.Token);
        }
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, source.Application!.Id);
        var result = await test.ExecuteAsync(ApplicationCloneAtomicityTests.Clone(test, source.Application.Id, source.Version!));
        Assert.Equal(409, result.Status);
        Assert.Null(result.Credential);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, source.Application.Id)).Digest);
        Assert.Empty(await database.Set<ApplicationCloneAuditDetail>().AsNoTracking().ToArrayAsync(test.Token));
    }
}
