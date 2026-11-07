using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Allows only three empty grant tables and exact history while comparing all preceding physical rows and metadata.</summary>
internal static class GrantUpgradeWitness
{
    internal const string PostgresMigration = "20261105154538_ApplicationGrantRevocation";
    internal const string SqlMigration = "20261105154811_ApplicationGrantRevocation";
    internal static readonly (string Schema, string Table)[] Tables =
        [("platform", "ApplicationGrantJob"), ("platform", "ApplicationGrantApproval"), ("platform", "ApplicationGrantAudit")];
    internal static readonly string[] Queries =
        ["SELECT * FROM \"platform\".\"ApplicationGrantJob\"", "SELECT * FROM \"platform\".\"ApplicationGrantApproval\"", "SELECT * FROM \"platform\".\"ApplicationGrantAudit\""];
    private const string Postgres = "PostgreSql";
    private const string TablePrefix = "SELECT * FROM \"";
    private const string Unexpected = "unexpected";

    /// <summary>Requires exact additive changes without adopting authority or altering old application, protocol or session data.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        Assert.Equal(Queries.Order(StringComparer.Ordinal), after.Keys.Except(before.Keys).Order(StringComparer.Ordinal));
        Assert.Empty(before.Keys.Except(after.Keys));
        foreach (var query in Queries)
        {
            Assert.Empty(after[query]);
        }
        foreach (var (query, rows) in before)
        {
            if (query == AdministratorUpgradeWitness.PlatformHistory)
            {
                BrowserAuditMigrationRows.RequireHistory(rows, after[query], provider == Postgres ? PostgresMigration : SqlMigration);
            }
            else if (query.StartsWith(TablePrefix, StringComparison.Ordinal))
            {
                Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(after[query]));
            }
            else
            {
                var additions = after[query].ToList();
                foreach (var row in rows)
                {
                    Assert.True(additions.Remove(row));
                }
                Assert.All(additions, row => Assert.True(Owns(row)));
            }
        }
    }

    /// <summary>Removes only independently named empty additions so earlier immutable upgrade witnesses still assess their original changes.</summary>
    internal static Dictionary<string, string[]> Predecessor(string provider, Dictionary<string, string[]> after)
    {
        after = PilotGateUpgradeWitness.Predecessor(provider, after);
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var migration = provider == Postgres ? PostgresMigration : SqlMigration;
        var removedHistory = 0;
        foreach (var (query, rows) in after)
        {
            if (Queries.Contains(query))
            {
                Assert.Empty(rows);
                continue;
            }
            if (query == AdministratorUpgradeWitness.PlatformHistory)
            {
                result[query] = rows.Where(row =>
                {
                    if (JsonSerializer.Deserialize<string?[]>(row)![0] != migration)
                    {
                        return true;
                    }
                    removedHistory++;
                    return false;
                }).ToArray();
            }
            else
            {
                result[query] = query.StartsWith(TablePrefix, StringComparison.Ordinal) ? rows : rows.Where(row => !Owns(row)).ToArray();
            }
        }
        Assert.Equal(1, removedHistory);
        Require(provider, result, after);
        return result;
    }

    /// <summary>Uses actual metadata schema/table fields rather than accepting resemblance to an object name.</summary>
    private static bool Owns(string row)
    {
        var values = JsonSerializer.Deserialize<string?[]>(row)!;
        return Tables.Any(owner => values.Length > 1 && values[0] == owner.Schema && values[1] == owner.Table);
    }

    /// <summary>Calibrates the real successful witness against lost history, damaged predecessor data and fabricated automatic jobs.</summary>
    internal static void RejectDefects(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        var history = new Dictionary<string, string[]>(after) { [AdministratorUpgradeWitness.PlatformHistory] = before[AdministratorUpgradeWitness.PlatformHistory] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, history));
        var changed = new Dictionary<string, string[]>(after) { [ManagedApplicationUpgradeWitness.PolicyQuery] = [Unexpected] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, changed));
        var adopted = new Dictionary<string, string[]>(after) { [Queries[0]] = [Unexpected] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, adopted));
    }
}
