using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Projects only independently named nullable additions, preserving every predecessor row and rejecting fabricated retained references.</summary>
internal static class ManagedScopeUpgradeWitness
{
    internal const string PostgresMigration = "20261114000928_ManagedScopePolicy";
    internal const string SqlMigration = "20261114000931_ManagedScopePolicy";
    internal const string ReferenceQuery = "SELECT * FROM \"platform\".\"ManagedScopeReference\"";
    internal const string InteractionQuery = "SELECT * FROM \"platform\".\"ManagedLoginInteraction\"";
    private const string Reference = "ManagedScopeReference";
    private const string Interaction = "ManagedLoginInteraction";
    private const string Constraint = "CK_ManagedLoginInteraction_Policy";
    private static readonly string[] Columns = ["PolicyDigest", "PolicySnapshot"];

    private const string Schema = "platform";
    private const int ColumnNameIndex = 5;
    private const int ColumnOrdinalIndex = 6;
    private const string Postgres = "PostgreSql";
    private const string TableQueryPrefix = "SELECT * FROM";
    private const string Nullable = "YES";
    private const int NullableIndex = 8;
    private const string PgText = "character varying";
    private const string SqlText = "nvarchar";
    private const int TypeIndex = 9;
    private const string DigestColumn = "PolicyDigest";
    private const string DigestWidth = "64";
    private const string PolicyWidth = "16384";
    private const string UnboundedSqlText = "-1";
    private const int LengthIndex = 10;
    private const string AudienceColumn = "AudienceId";
    private const string ScopeColumn = "ScopeId";
    private const string SelectorColumn = "SelectorDigest";

    /// <summary>Requires exactly the new history, nullable columns and empty reference table before removing those additions for older witnesses.</summary>
    internal static Dictionary<string, string[]> Predecessor(string provider, Dictionary<string, string[]> after)
    {
        Assert.Empty(after[ReferenceQuery]);
        RequireColumns(provider, after[DatabaseSnapshot.Columns]);
        ManagedScopeSchemaIndexes.Require(provider, after);
        ManagedScopeSchemaReferences.Require(provider, after);
        var columnRows = after[DatabaseSnapshot.Columns].Select(Parse).Where(row => row[0] == Schema && row[1] == Interaction && Columns.Contains(row[ColumnNameIndex])).ToArray();
        var positions = columnRows.Select(row => int.Parse(row[ColumnOrdinalIndex]!, System.Globalization.CultureInfo.InvariantCulture) - 1).ToArray();
        var migration = provider == Postgres ? PostgresMigration : SqlMigration;
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (query, rows) in after)
        {
            if (query == ReferenceQuery)
            {
                continue;
            }
            result[query] = query == InteractionQuery ? rows.Select(row => Strip(row, positions)).Order(StringComparer.Ordinal).ToArray() :
                query == AdministratorUpgradeWitness.PlatformHistory ? rows.Where(row => Parse(row)[0] != migration).ToArray() :
                query.StartsWith(TableQueryPrefix, StringComparison.Ordinal) ? rows : rows.Where(row => !Owns(query, row)).ToArray();
        }
        BrowserAuditMigrationRows.RequireHistory(result[AdministratorUpgradeWitness.PlatformHistory], after[AdministratorUpgradeWitness.PlatformHistory], migration);
        return result;
    }

    /// <summary>Compares the complete predecessor physical state after a validated narrow projection, including unchanged histories and metadata.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        var projected = Predecessor(provider, after);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), projected.Keys.Order(StringComparer.Ordinal));
        foreach (var (query, rows) in before)
        {
            Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(projected[query]));
        }
    }

    /// <summary>Checks independent nullable storage facets and exact reference-column shape without consulting production EF metadata.</summary>
    internal static void RequireColumns(string provider, string[] rows)
    {
        var values = rows.Select(Parse).Where(row => row[0] == Schema).ToArray();
        var policy = values.Where(row => row[1] == Interaction && Columns.Contains(row[ColumnNameIndex])).ToArray();
        Assert.Equal(Columns, policy.Select(row => row[ColumnNameIndex]).Order(StringComparer.Ordinal));
        foreach (var column in policy)
        {
            Assert.Equal(Nullable, column[NullableIndex]);
            Assert.Equal(provider == Postgres ? PgText : SqlText, column[TypeIndex]);
            Assert.Equal(column[ColumnNameIndex] == DigestColumn ? DigestWidth : provider == Postgres ? PolicyWidth : UnboundedSqlText, column[LengthIndex]);
        }
        Assert.Equal([AudienceColumn, ScopeColumn, SelectorColumn], values.Where(row => row[1] == Reference).Select(row => row[ColumnNameIndex]).Order(StringComparer.Ordinal));
    }

    /// <summary>Calibrates the exact projection against missing indexes/parents, wrong nullability and fabricated policy on historical rows.</summary>
    internal static void RejectDefects(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        const string ReferenceMetadata = "information_schema.referential_constraints";
        const string NotNullable = "NO";
        const string Invented = "invented-policy";
        ManagedScopeSchemaIndexes.RejectDefects(provider, after);
        var query = Assert.Single(after.Keys, value => value.Contains(ReferenceMetadata, StringComparison.Ordinal));
        var missing = new Dictionary<string, string[]>(after) { [query] = after[query].Where(row => Parse(row)[1] != Reference).ToArray() };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, missing));
        var columns = after[DatabaseSnapshot.Columns].Select(row =>
        {
            var values = Parse(row);
            if (values[1] == Interaction && values[ColumnNameIndex] == DigestColumn)
            {
                values[NullableIndex] = NotNullable;
            }
            return JsonSerializer.Serialize(values);
        }).ToArray();
        var widened = new Dictionary<string, string[]>(after) { [DatabaseSnapshot.Columns] = columns };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, widened));
        var ordinal = int.Parse(Assert.Single(after[DatabaseSnapshot.Columns].Select(Parse), row => row[1] == Interaction &&
            row[ColumnNameIndex] == DigestColumn)[ColumnOrdinalIndex]!, System.Globalization.CultureInfo.InvariantCulture) - 1;
        var first = Parse(after[InteractionQuery][0]);
        first[ordinal] = Invented;
        var adopted = new Dictionary<string, string[]>(after) { [InteractionQuery] = [JsonSerializer.Serialize(first), .. after[InteractionQuery].Skip(1)] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, adopted));
    }

    /// <summary>Refuses any migrated policy value instead of quietly treating a populated new column as legacy null.</summary>
    private static string Strip(string row, int[] positions)
    {
        var values = Parse(row);
        Assert.All(positions, position => Assert.Null(values[position]));
        return JsonSerializer.Serialize(values.Where((_, index) => !positions.Contains(index)));
    }

    /// <summary>Recognizes only metadata belonging to the new table, exact policy columns or exact paired-null constraint.</summary>
    private static bool Owns(string query, string row)
    {
        var values = Parse(row);
        return values[0] == Schema && (values[1] == Reference || values[1] == Interaction &&
            (query == DatabaseSnapshot.Columns ? Columns.Contains(values[ColumnNameIndex]) : values.Contains(Constraint)));
    }

    /// <summary>Reads the existing independently captured physical-row encoding.</summary>
    private static string?[] Parse(string row) => JsonSerializer.Deserialize<string?[]>(row)!;
}
