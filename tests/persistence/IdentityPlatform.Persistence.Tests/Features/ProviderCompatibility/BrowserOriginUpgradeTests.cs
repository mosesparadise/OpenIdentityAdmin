using System.Data.Common;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Verifies the independent origin relation and the populated downgrade guard on both supported engines.</summary>
public sealed class BrowserOriginUpgradeTests
{
    private const string Postgres = "PostgreSql";
    private const string Sql = "SqlServer";
    private const string ProviderTrait = "Provider";
    private const string BrowserProfile = "managed-browser-v1";
    private const string SecondOrigin = "https://second.example";
    private const string ThirdOrigin = "https://third.example";
    private const string TableRead = "SELECT * FROM";
    private const string Schema = "platform";
    private const int LastSlot = 9;
    private const int FirstInvalidSlot = 10;

    internal const string PostgresMigration = "20261127234604_ManagedBrowserOrigins";
    internal const string SqlMigration = "20261127234607_ManagedBrowserOrigins";
    private const string Table = "ApplicationBrowserOrigin";
    private const string Query = "SELECT * FROM \"platform\".\"ApplicationBrowserOrigin\"";
    private const string Origin = "https://portal.example";

    /// <summary>Requires predecessor data to survive an empty round trip and protects an explicitly authorized origin against rollback.</summary>
    [Theory]
    [InlineData(Postgres, Traits = [ProviderTrait, Postgres])]
    [InlineData(Sql, Traits = [ProviderTrait, Sql])]
    public async Task Given_ExplicitBrowserOrigins_When_UpgradingAndRollingBack_Then_BindingsAreUniqueBoundedAndRetained(string provider)
    {
        var target = provider == Postgres ? PostgresMigration : SqlMigration;
        var prior = provider == Postgres ? ProfileStorageWitness.PostgresMigration : ProfileStorageWitness.SqlMigration;
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider, target: prior);
        var id = ManagedSchemaRows.Digest();
        await using (var scope = fixture.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            database.Add(new ApplicationPolicy { ApplicationId = id, Profile = BrowserProfile, Revision = 1 });
            await database.SaveChangesAsync(fixture.Token);
        }
        var before = await fixture.SnapshotAsync(provider);
        await fixture.MigrateAsync(target);
        RequireEqual(before, Predecessor(provider, await fixture.SnapshotAsync(provider)));
        await fixture.MigrateAsync(prior);
        RequireEqual(before, await fixture.SnapshotAsync(provider));
        await fixture.MigrateAsync(target);
        await InsertAsync(fixture, id, 0, Origin);
        await InsertAsync(fixture, id, LastSlot, SecondOrigin);
        await Assert.ThrowsAsync<DbUpdateException>(() => InsertAsync(fixture, id, 1, Origin));
        await Assert.ThrowsAsync<DbUpdateException>(() => InsertAsync(fixture, id, 0, ThirdOrigin));
        await Assert.ThrowsAsync<DbUpdateException>(() => InsertAsync(fixture, id, FirstInvalidSlot, ThirdOrigin));
        await Assert.ThrowsAsync<DbUpdateException>(() => InsertAsync(fixture, id, -1, ThirdOrigin));
        await Assert.ThrowsAsync<DbUpdateException>(() => InsertAsync(fixture, ManagedSchemaRows.Digest(), 1, Origin));
        var bound = await fixture.SnapshotAsync(provider);
        await Assert.ThrowsAnyAsync<DbException>(() => fixture.MigrateAsync(prior));
        await fixture.MigrateAsync(target);
        RequireEqual(bound, await fixture.SnapshotAsync(provider));
    }

    /// <summary>Projects only the verified empty origin table and the single known migration away for predecessor comparisons.</summary>
    internal static Dictionary<string, string[]> Predecessor(string provider, Dictionary<string, string[]> after)
    {
        if (DeviceKeyUpgradeTests.Present(after))
        {
            after = DeviceKeyUpgradeTests.Predecessor(provider, after);
        }
        Assert.Empty(after[Query]);
        var migration = provider == Postgres ? PostgresMigration : SqlMigration;
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (query, rows) in after)
        {
            if (query != Query)
            {
                result[query] = query == AdministratorUpgradeWitness.PlatformHistory ? rows.Where(row => Parse(row)[0] != migration).ToArray() :
                    query.StartsWith(TableRead, StringComparison.Ordinal) ? rows : rows.Where(row => Parse(row) is not [Schema, Table, ..]).ToArray();
            }
        }
        BrowserAuditMigrationRows.RequireHistory(result[AdministratorUpgradeWitness.PlatformHistory], after[AdministratorUpgradeWitness.PlatformHistory], migration);
        return result;
    }

    /// <summary>Inserts a single origin in a fresh context to exercise real constraints after previous failed writes.</summary>
    private static async Task InsertAsync(ManagedSchemaFixture fixture, string id, int slot, string origin)
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Add(new ApplicationBrowserOrigin { ApplicationId = id, Slot = slot, Origin = origin });
        await database.SaveChangesAsync(fixture.Token);
    }

    /// <summary>Compares every metadata and data row without exposing any values.</summary>
    private static void RequireEqual(Dictionary<string, string[]> expected, Dictionary<string, string[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (query, rows) in expected)
        {
            Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(actual[query]));
        }
    }

    /// <summary>Reads only independently captured physical metadata.</summary>
    private static string?[] Parse(string row) => JsonSerializer.Deserialize<string?[]>(row)!;
}
