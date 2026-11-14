using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Verifies real immediate-predecessor upgrades preserve existing native rows and legacy interactions, with no invented consent or references.</summary>
public sealed class ManagedScopeUpgradeTests
{
    private const string Insert = "INSERT INTO \"platform\".\"ManagedLoginInteraction\" (\"SelectorDigest\", \"BrowserDigest\", \"DemandDigest\", " +
        "\"ApplicationId\", \"ApplicationRevision\", \"Revision\", \"State\", \"CreatedAt\", \"ExpiresAt\", \"ObservedAt\", \"RetainUntil\", \"CorrelationId\") " +
        "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11})";

    private const string Postgres = "PostgreSql";
    private const string UnexpectedReference = "unexpected";
    private const int Expired = 7;
    private const string LegacyApplication = "synthetic-policy-legacy";
    private const long CreatedAt = 1000L;
    private const long ExpiresAt = 1300L;
    private const long RetainUntil = 1210600L;
    private const string NativeScope = "synthetic.policy.predecessor";
    private const string NativeDisplay = "Existing native scope";
    private const string NativeResource = "synthetic-native-resource";

    /// <summary>Runs the actual Migrator twice and compares every original row and metadata entry through a strictly calibrated nullable-addition witness.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PopulatedCataloguePredecessor_When_PolicyMigrationRunsTwice_Then_LegacyAndNativeStateRemainExact(string provider)
    {
        await using var test = await ManagedSchemaFixture.CreateAsync(provider, target: provider == Postgres ?
            CatalogueUpgradeWitness.PostgresMigration : CatalogueUpgradeWitness.SqlMigration);
        await SeedAsync(test);
        var before = await test.SnapshotAsync(provider);
        Assert.Equal(2, before[ManagedScopeUpgradeWitness.InteractionQuery].Length);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(test.Services, test.Token));
        var after = await test.SnapshotAsync(provider);
        ManagedScopeUpgradeWitness.Require(provider, before, after);
        ManagedScopeUpgradeWitness.RejectDefects(provider, before, after);
        var damaged = new Dictionary<string, string[]>(after) { [ManagedScopeUpgradeWitness.ReferenceQuery] = [UnexpectedReference] };
        Assert.ThrowsAny<XunitException>(() => ManagedScopeUpgradeWitness.Require(provider, before, damaged));
        damaged = new(after)
        {
            [ManagedScopeUpgradeWitness.InteractionQuery] = []
        };
        Assert.ThrowsAny<XunitException>(() => ManagedScopeUpgradeWitness.Require(provider, before, damaged));
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(test.Services, test.Token));
        ManagedScopeUpgradeWitness.Require(provider, before, await test.SnapshotAsync(provider));
        await using var observer = test.Scope();
        var rows = await observer.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ManagedLoginInteraction>().AsNoTracking().ToArrayAsync(test.Token);
        Assert.All(rows, row => { Assert.Null(row.PolicySnapshot); Assert.Null(row.PolicyDigest); Assert.True(ManagedScopeBinding.IsOpenId(row)); });
    }

    /// <summary>Inserts only independently named predecessor fields and actual native scope/resource data, without using the new EF interaction shape.</summary>
    private static async Task SeedAsync(ManagedSchemaFixture test)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        foreach (var state in new[] { 1, Expired })
        {
            await database.Database.ExecuteSqlRawAsync(Insert, [ManagedSchemaRows.Digest(), ManagedSchemaRows.Digest(), ManagedSchemaRows.Digest(),
                LegacyApplication, 1L, 1L, state, CreatedAt, ExpiresAt, CreatedAt, RetainUntil, Guid.NewGuid()], test.Token);
        }
        var descriptor = new OpenIddictScopeDescriptor { Name = NativeScope, DisplayName = NativeDisplay };
        descriptor.Resources.Add(NativeResource);
        await scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>().CreateAsync(descriptor, test.Token);
    }
}
