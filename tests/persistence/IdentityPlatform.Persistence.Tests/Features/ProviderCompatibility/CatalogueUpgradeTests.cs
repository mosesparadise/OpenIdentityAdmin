using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Proves additive catalogue installation preserves populated native scopes, managed application/grant state and both physical histories.</summary>
public sealed class CatalogueUpgradeTests
{
    private const string Postgres = "PostgreSql";
    private const string NativeScope = "synthetic-pre-catalogue-scope";
    private const string NativeResource = "synthetic-pre-catalogue-resource";
    private const string NativeDisplay = "Pre-existing unmanaged scope";

    /// <summary>Uses the actual immediate predecessor and actual Migrator twice, checking original rows/metadata and rejecting corrupted witnesses.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PopulatedPilotGateSchema_When_CatalogueMigratesTwice_Then_HistoryAndNativeStateRemainExact(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider, target: provider == Postgres ?
            PilotGateUpgradeWitness.PostgresMigration : PilotGateUpgradeWitness.SqlMigration);
        await SeedAsync(fixture);
        var before = await fixture.SnapshotAsync(provider);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(fixture.Services, fixture.Token));
        var after = ManagedScopeUpgradeWitness.Predecessor(provider, await fixture.SnapshotAsync(provider));
        CatalogueUpgradeWitness.Require(provider, before, after);
        CatalogueUpgradeWitness.RejectDefects(provider, before, after);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(fixture.Services, fixture.Token));
        var repeated = ManagedScopeUpgradeWitness.Predecessor(provider, await fixture.SnapshotAsync(provider));
        CatalogueUpgradeWitness.Require(provider, before, repeated);
        foreach (var (query, rows) in after)
        {
            Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(repeated[query]));
        }
    }

    /// <summary>Seeds real native resource data and prior security-job history without creating or adopting any new catalogue policy.</summary>
    private static async Task SeedAsync(ManagedSchemaFixture fixture)
    {
        await using var scope = fixture.Scope();
        var services = scope.ServiceProvider;
        var manager = services.GetRequiredService<IOpenIddictScopeManager>();
        var descriptor = new OpenIddictScopeDescriptor { Name = NativeScope, DisplayName = NativeDisplay };
        descriptor.Resources.Add(NativeResource);
        await manager.CreateAsync(descriptor, fixture.Token);
        var database = services.GetRequiredService<PlatformDbContext>();
        GrantSchemaRows.Add(database);
        await database.SaveChangesAsync(fixture.Token);
        Assert.NotEmpty(await database.Set<IdentityPlatform.Infrastructure.Features.Applications.ApplicationPolicy>().AsNoTracking().ToListAsync(fixture.Token));
    }
}
