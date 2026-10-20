using System.Text.Json;
using IdentityPlatform.Database;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Allows exactly the approved additive protocol tables/history while preserving every old schema/data evidence row.</summary>
internal static class AdministratorUpgradeWitness
{
    private const string TableQueryPrefix = "SELECT * FROM \"";
    private const string PlatformHistory = "SELECT * FROM \"platform\".\"PlatformMigrations\"";
    private const string SessionHistory = "SELECT * FROM \"bff\".\"SessionMigrations\"";
    private const string ProductVersion = "10.0.12";
    private const int BudgetIndex = 2;
    private static readonly string[] InitialBudget = ["1", "0"];
    private static readonly (string Schema, string Table)[] AddedTables = [("platform", "AdministratorProtocolProfile"),
        ("platform", "AdministratorClientAssertionUse"), ("bff", "AdministratorClientStateBudget")];
    private static readonly string[] AddedQueries = ["SELECT * FROM \"platform\".\"AdministratorProtocolProfile\"",
        "SELECT * FROM \"platform\".\"AdministratorClientAssertionUse\"", "SELECT * FROM \"bff\".\"AdministratorClientStateBudget\""];

    /// <summary>Checks all old objects and data, exact new history rows and inert initialization rather than refreshing the frozen producer.</summary>
    internal static void Require(IRelationalDatabaseProvider provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        Assert.Equal(AddedQueries.Order(StringComparer.Ordinal), after.Keys.Except(before.Keys).Order(StringComparer.Ordinal));
        Assert.Empty(before.Keys.Except(after.Keys));
        foreach (var (query, rows) in before)
        {
            if (query is PlatformHistory or SessionHistory)
            {
                var store = query == PlatformHistory ? DatabaseStore.Platform : DatabaseStore.Sessions;
                Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(after[query].Intersect(rows).Order(StringComparer.Ordinal)));
                Assert.Equal(JsonSerializer.Serialize(new[] { provider.Migrations(store)[^1], ProductVersion }), Assert.Single(after[query].Except(rows)));
            }
            else if (query.StartsWith(TableQueryPrefix, StringComparison.Ordinal))
            {
                Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(after[query]));
            }
            else
            {
                RequireCatalog(rows, after[query]);
            }
        }
        Assert.Empty(after[AddedQueries[0]]);
        Assert.Empty(after[AddedQueries[1]]);
        Assert.Equal(JsonSerializer.Serialize(InitialBudget), Assert.Single(after[AddedQueries[BudgetIndex]]));
    }

    /// <summary>Retains duplicate old metadata and accepts new rows only with unambiguous independently queried schema/table provenance.</summary>
    internal static void RequireCatalog(string[] before, string[] after)
    {
        var additions = after.ToList();
        foreach (var row in before)
        {
            Assert.True(additions.Remove(row));
        }
        foreach (var row in additions)
        {
            var values = JsonSerializer.Deserialize<string?[]>(row)!;
            Assert.Contains(AddedTables, owner => owner.Schema == values[0] && owner.Table == values[1]);
        }
    }
}
