using System.Globalization;
using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Projects only independently validated retirement additions, retaining every predecessor row, metadata field and history entry.</summary>
internal static class RetirementUpgradeWitness
{
    internal const string PostgresMigration = "20261115013945_ApplicationRetirement";
    internal const string SqlMigration = "20261115014331_ApplicationRetirement";
    internal const string Query = "SELECT * FROM \"platform\".\"ApplicationRetirement\"";
    internal const string Table = "ApplicationRetirement";
    internal const string Policy = "ApplicationPolicy";
    internal const string Schema = "platform";
    internal const string Retired = "Retired";
    internal const string TerminalCheck = "CK_ApplicationPolicy_Retirement";
    internal const string NotNull = "ApplicationPolicy_Retired_not_null";
    private const string Postgres = "PostgreSql";
    private const string TablePrefix = "SELECT * FROM";
    private const string False = "False";
    private const string True = "True";
    private const string Unexpected = "unexpected-retirement";
    private const int ColumnName = 5;
    private const int ColumnOrdinal = 6;
    private const int ConstraintName = 4;

    /// <summary>Rejects migration-created retirements and requires false default on every existing policy before removing only the new physical field.</summary>
    internal static Dictionary<string, string[]> Predecessor(string provider, Dictionary<string, string[]> after)
    {
        Assert.Empty(after[Query]);
        RetirementUpgradeSchema.Require(provider, after);
        var ordinal = Position(after);
        var migration = provider == Postgres ? PostgresMigration : SqlMigration;
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (query, rows) in after)
        {
            if (query == Query)
            {
                continue;
            }
            result[query] = query == ManagedApplicationUpgradeWitness.PolicyQuery ? rows.Select(row => Strip(row, ordinal)).Order(StringComparer.Ordinal).ToArray() :
                query == AdministratorUpgradeWitness.PlatformHistory ? rows.Where(row => Parse(row)[0] != migration).ToArray() :
                query.StartsWith(TablePrefix, StringComparison.Ordinal) ? rows : rows.Where(row => !Owns(query, row)).ToArray();
        }
        BrowserAuditMigrationRows.RequireHistory(result[AdministratorUpgradeWitness.PlatformHistory], after[AdministratorUpgradeWitness.PlatformHistory], migration);
        return result;
    }

    /// <summary>Compares every original physical fingerprint including nonempty clone provenance and grant approval history.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        var projected = Predecessor(provider, after);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), projected.Keys.Order(StringComparer.Ordinal));
        foreach (var (query, rows) in before)
        {
            Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(projected[query]));
        }
    }

    /// <summary>Calibrates preservation against fabricated retirement, altered old state/history and an already-retired migrated policy.</summary>
    internal static void RejectDefects(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        var adopted = new Dictionary<string, string[]>(after) { [Query] = [Unexpected] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, adopted));
        foreach (var query in new[] { AdministratorUpgradeWitness.PlatformHistory, ManagedApplicationUpgradeWitness.PolicyQuery, CloneUpgradeWitness.Query })
        {
            Assert.NotEmpty(after[query]);
            var changed = new Dictionary<string, string[]>(after) { [query] = after[query].Skip(1).ToArray() };
            Assert.ThrowsAny<XunitException>(() => Require(provider, before, changed));
        }
        var rows = after[ManagedApplicationUpgradeWitness.PolicyQuery].ToArray();
        var first = Parse(rows[0]);
        first[Position(after)] = True;
        rows[0] = JsonSerializer.Serialize(first);
        var retired = new Dictionary<string, string[]>(after) { [ManagedApplicationUpgradeWitness.PolicyQuery] = rows };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, retired));
        RetirementUpgradeSchema.RejectDefects(provider, after);
    }

    /// <summary>Uses independently captured physical column ordinal rather than EF property order or an assumed append position.</summary>
    private static int Position(Dictionary<string, string[]> snapshot) => int.Parse(Assert.Single(snapshot[DatabaseSnapshot.Columns].Select(Parse),
        row => row[0] == Schema && row[1] == Policy && row[ColumnName] == Retired)[ColumnOrdinal]!, CultureInfo.InvariantCulture) - 1;

    /// <summary>Removes only a verified false migration default and keeps all earlier values in their original order.</summary>
    private static string Strip(string row, int position)
    {
        var values = Parse(row);
        Assert.Equal(False, values[position]);
        return JsonSerializer.Serialize(values.Where((_, index) => index != position));
    }

    /// <summary>Recognizes exact new table, column and constraint ownership; unrelated old policy metadata remains compared.</summary>
    private static bool Owns(string query, string row)
    {
        var values = Parse(row);
        return values[0] == Schema && (values[1] == Table || values[1] == Policy &&
            (query == DatabaseSnapshot.Columns ? values[ColumnName] == Retired : values[ConstraintName] is TerminalCheck or NotNull));
    }

    /// <summary>Reads the existing physical snapshot format without candidate model metadata.</summary>
    internal static string?[] Parse(string row) => JsonSerializer.Deserialize<string?[]>(row)!;
}
