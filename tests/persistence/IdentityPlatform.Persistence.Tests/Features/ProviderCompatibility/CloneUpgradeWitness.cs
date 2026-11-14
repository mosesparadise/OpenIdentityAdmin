using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Validates the empty clone provenance addition before projecting every retained physical row and metadata entry.</summary>
internal static class CloneUpgradeWitness
{
    internal const string PostgresMigration = "20261114231736_ApplicationCloning";
    internal const string SqlMigration = "20261114231822_ApplicationCloning";
    internal const string Query = "SELECT * FROM \"platform\".\"ApplicationCloneAuditDetail\"";
    private const string Schema = "platform";
    private const string Table = "ApplicationCloneAuditDetail";
    private const string Postgres = "PostgreSql";
    private const string TablePrefix = "SELECT * FROM";
    private const string Unexpected = "unexpected";

    /// <summary>Removes only the validated empty new relation and one exact history entry; old objects remain byte-for-byte observable.</summary>
    internal static Dictionary<string, string[]> Predecessor(string provider, Dictionary<string, string[]> after)
    {
        Assert.Empty(after[Query]);
        CloneUpgradeSchema.Require(provider, after);
        var migration = provider == Postgres ? PostgresMigration : SqlMigration;
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (query, rows) in after)
        {
            if (query == Query)
            {
                continue;
            }
            result[query] = query == AdministratorUpgradeWitness.PlatformHistory ? rows.Where(row => Parse(row)[0] != migration).ToArray() :
                query.StartsWith(TablePrefix, StringComparison.Ordinal) ? rows : rows.Where(row => !Owns(row)).ToArray();
        }
        BrowserAuditMigrationRows.RequireHistory(result[AdministratorUpgradeWitness.PlatformHistory], after[AdministratorUpgradeWitness.PlatformHistory], migration);
        return result;
    }

    /// <summary>Requires all predecessor tables, history and metadata to survive exactly after narrowly validating the addition.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        var projected = Predecessor(provider, after);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), projected.Keys.Order(StringComparer.Ordinal));
        foreach (var (query, rows) in before)
        {
            Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(projected[query]));
        }
    }

    /// <summary>Calibrates the witness against fabricated provenance, missing history and changed existing application rows.</summary>
    internal static void RejectDefects(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        var adopted = new Dictionary<string, string[]>(after) { [Query] = [Unexpected] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, adopted));
        var lost = new Dictionary<string, string[]>(after) { [AdministratorUpgradeWitness.PlatformHistory] = before[AdministratorUpgradeWitness.PlatformHistory] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, lost));
        var changed = new Dictionary<string, string[]>(after) { [ManagedApplicationUpgradeWitness.PolicyQuery] = [Unexpected] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, changed));
        CloneUpgradeSchema.RejectDefects(provider, after);
    }

    /// <summary>Recognizes only exact metadata ownership, never text that merely mentions the new table.</summary>
    private static bool Owns(string row)
    {
        var values = Parse(row);
        return values[0] == Schema && values[1] == Table;
    }

    /// <summary>Reads the independent physical database snapshot encoding.</summary>
    private static string?[] Parse(string row) => JsonSerializer.Deserialize<string?[]>(row)!;
}
