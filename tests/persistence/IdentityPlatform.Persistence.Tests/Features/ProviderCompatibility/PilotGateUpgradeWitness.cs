using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Allows only the seeded coordination table and exact history while comparing all preceding physical rows and metadata.</summary>
internal static class PilotGateUpgradeWitness
{
    private const string ColumnName = "Id";
    private const string Required = "NO";
    private const string PostgresType = "character varying";
    private const string SqlType = "nvarchar";
    private const string MaximumLength = "32";
    private const string KeyQuery = "information_schema.key_column_usage";
    private const string ConstraintQuery = "FROM information_schema.table_constraints";
    private const string ReferenceQuery = "FROM information_schema.referential_constraints";
    private const string PrimaryKey = "PRIMARY KEY";
    private const int ColumnNameIndex = 5;
    private const int NullableIndex = 8;
    private const int TypeIndex = 9;
    private const int LengthIndex = 10;
    private const int KeyColumnIndex = 8;
    private const int ConstraintTypeIndex = 8;

    internal const string PostgresMigration = "20261107135812_PilotProtocolGate";
    internal const string SqlMigration = "20261107140026_PilotProtocolGate";
    internal static readonly (string Schema, string Table)[] Tables = [("platform", "PilotProtocolGate")];
    internal static readonly string[] Queries = ["SELECT * FROM \"platform\".\"PilotProtocolGate\""];
    private static readonly string[] Seed = [JsonSerializer.Serialize(new[] { "first-party-v1" })];
    private const string Postgres = "PostgreSql";
    private const string TablePrefix = "SELECT * FROM \"";
    private const string Unexpected = "unexpected";

    /// <summary>Requires exact additive changes without granting pilot authority or altering old application, protocol or session data.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        RequireSchema(provider, after);
        Assert.Equal(Queries.Order(StringComparer.Ordinal), after.Keys.Except(before.Keys).Order(StringComparer.Ordinal));
        Assert.Empty(before.Keys.Except(after.Keys));
        foreach (var query in Queries)
        {
            Assert.Equal(Seed, after[query]);
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

    /// <summary>Checks independently queried physical column and key bounds and forbids authority/native foreign-key dependencies.</summary>
    internal static void RequireSchema(string provider, Dictionary<string, string[]> snapshot)
    {
        var column = JsonSerializer.Deserialize<string?[]>(Assert.Single(snapshot[DatabaseSnapshot.Columns], Owns))!;
        Assert.Equal(ColumnName, column[ColumnNameIndex]);
        Assert.Equal(Required, column[NullableIndex]);
        Assert.Equal(provider == Postgres ? PostgresType : SqlType, column[TypeIndex]);
        Assert.Equal(MaximumLength, column[LengthIndex]);
        var keys = snapshot.Single(pair => pair.Key.Contains(KeyQuery, StringComparison.Ordinal)).Value;
        Assert.Equal(ColumnName, JsonSerializer.Deserialize<string?[]>(Assert.Single(keys, Owns))![KeyColumnIndex]);
        var constraints = snapshot.Single(pair => pair.Key.Contains(ConstraintQuery, StringComparison.Ordinal)).Value;
        Assert.Single(constraints.Where(Owns).Select(row => JsonSerializer.Deserialize<string?[]>(row)!)
            , values => values[ConstraintTypeIndex] == PrimaryKey);
        var references = snapshot.Single(pair => pair.Key.Contains(ReferenceQuery, StringComparison.Ordinal)).Value;
        Assert.DoesNotContain(references, Owns);
    }

    /// <summary>Removes only independently named seeded additions so earlier immutable upgrade witnesses still assess their original changes.</summary>
    internal static Dictionary<string, string[]> Predecessor(string provider, Dictionary<string, string[]> after)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var migration = provider == Postgres ? PostgresMigration : SqlMigration;
        var removedHistory = 0;
        foreach (var (query, rows) in after)
        {
            if (Queries.Contains(query))
            {
                Assert.Equal(Seed, rows);
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

    /// <summary>Calibrates the real successful witness against lost history, damaged predecessor data and damaged gate seeds.</summary>
    internal static void RejectDefects(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        var history = new Dictionary<string, string[]>(after) { [AdministratorUpgradeWitness.PlatformHistory] = before[AdministratorUpgradeWitness.PlatformHistory] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, history));
        var changed = new Dictionary<string, string[]>(after) { [ManagedApplicationUpgradeWitness.PolicyQuery] = [Unexpected] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, changed));
        var progress = new Dictionary<string, string[]>(after) { [GrantUpgradeWitness.Queries[0]] = [] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, progress));
        var adopted = new Dictionary<string, string[]>(after) { [Queries[0]] = [Unexpected] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, adopted));
    }
}
