using System.Text.Json;
using IdentityPlatform.Database;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Allows exactly the approved additive protocol tables/history while preserving every old schema/data evidence row.</summary>
internal static class AdministratorUpgradeWitness
{
    private const string TableQueryPrefix = "SELECT * FROM \"";
    internal const string PlatformHistory = "SELECT * FROM \"platform\".\"PlatformMigrations\"";
    internal const string SessionHistory = "SELECT * FROM \"bff\".\"SessionMigrations\"";
    private const int BudgetIndex = 2;
    private const int ContinuationIndex = 3;
    private static readonly string[] InitialBudget = ["1", "0"];
    private static readonly (string Schema, string Table)[] AddedTables = [("platform", "AdministratorProtocolProfile"),
        ("platform", "AdministratorClientAssertionUse"), ("bff", "AdministratorClientStateBudget"), ("platform", "AdministratorIssuerContinuation"), .. BrowserCoordinationUpgradeWitness.Tables, .. ManagedApplicationUpgradeWitness.Tables];
    private static readonly string[] AddedQueries = ["SELECT * FROM \"platform\".\"AdministratorProtocolProfile\"",
        "SELECT * FROM \"platform\".\"AdministratorClientAssertionUse\"", "SELECT * FROM \"bff\".\"AdministratorClientStateBudget\"", IssuerContinuationUpgradeWitness.ContinuationQuery, .. BrowserCoordinationUpgradeWitness.Queries, .. ManagedApplicationUpgradeWitness.Queries];

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
                IssuerContinuationUpgradeWitness.RequireHistory(provider, store, after[query].Except(rows));
            }
            else if (query == IssuerContinuationUpgradeWitness.InteractionQuery)
            {
                IssuerContinuationUpgradeWitness.RequireRows(rows, after[query],
                    before[DatabaseSnapshot.Columns].Count(IssuerContinuationUpgradeWitness.IsInteraction));
            }
            else if (query == BrowserAuditUpgradeWitness.Query)
            {
                IssuerContinuationUpgradeWitness.RequireRows(rows, after[query],
                    before[DatabaseSnapshot.Columns].Count(BrowserAuditUpgradeWitness.IsAudit));
            }
            else if (query.StartsWith(TableQueryPrefix, StringComparison.Ordinal))
            {
                Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(after[query]));
            }
            else
            {
                RequireCatalog(rows, after[query], query, provider.Id);
            }
        }
        BrowserCoordinationUpgradeWitness.RequireSeeds(after);
        ManagedApplicationUpgradeWitness.RequireEmpty(after);
        Assert.Empty(after[AddedQueries[0]]);
        Assert.Empty(after[AddedQueries[1]]);
        Assert.Empty(after[AddedQueries[ContinuationIndex]]);
        Assert.Equal(JsonSerializer.Serialize(InitialBudget), Assert.Single(after[AddedQueries[BudgetIndex]]));
    }

    /// <summary>Retains duplicate old metadata and accepts new rows only with unambiguous independently queried schema/table provenance.</summary>
    internal static void RequireCatalog(string[] before, string[] after, string? query = null, string? provider = null)
    {
        var additions = after.ToList();
        foreach (var row in before)
        {
            Assert.True(additions.Remove(row));
        }
        if (query == DatabaseSnapshot.Columns)
        {
            var column = Assert.Single(additions, IssuerContinuationUpgradeWitness.IsInteraction);
            IssuerContinuationUpgradeWitness.RequireColumn(provider!, column, before);
            Assert.True(additions.Remove(column));
            BrowserAuditUpgradeWitness.RemoveColumn(additions, before, provider!);
        }
        foreach (var row in additions)
        {
            var values = JsonSerializer.Deserialize<string?[]>(row)!;
            Assert.Contains(AddedTables, owner => owner.Schema == values[0] && owner.Table == values[1]);
        }
    }
}
