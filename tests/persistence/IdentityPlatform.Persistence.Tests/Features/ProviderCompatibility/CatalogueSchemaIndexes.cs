using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Independently requires every catalogue index, its uniqueness and ordered key columns from physical engine metadata.</summary>
internal static class CatalogueSchemaIndexes
{
    private const string Postgres = "PostgreSql";
    private const string Audience = "ApiAudience";
    private const string Policy = "ScopePolicy";
    private const string Job = "CatalogueDeletion";
    private const string Schema = "platform";
    private const string UniqueIndex = "CREATE UNIQUE INDEX ";
    private const string True = "True";
    private const string False = "False";
    private const string Separator = ", ";
    private const int PgNameIndex = 4;
    private const int PgDefinitionIndex = 6;
    private const int SqlNameIndex = 3;
    private const int SqlUniqueIndex = 7;
    private const int SqlColumnOffset = 5;
    private const int SqlOrdinalOffset = 4;
    private const int ColumnNameIndex = 5;
    private const int ColumnOrdinalIndex = 6;
    private static readonly (string Table, string Name, bool Unique, string[] Columns)[] Expected =
    [
        (Audience, "PK_ApiAudience", true, ["Id"]),
        (Audience, "IX_ApiAudience_Resource", true, ["Resource"]),
        (Policy, "PK_ScopePolicy", true, ["ScopeId"]),
        (Job, "PK_CatalogueDeletion", true, ["Id"]),
        (Job, "IX_CatalogueDeletion_AdmissionAuditId", true, ["AdmissionAuditId"]),
        (Job, "IX_CatalogueDeletion_CompletionAuditId", true, ["CompletionAuditId"]),
        (Job, "IX_CatalogueDeletion_ScopedKeyHash", true, ["ScopedKeyHash"]),
        (Job, "IX_CatalogueDeletion_Kind_TargetId_State", false, ["Kind", "TargetId", "State"]),
        (Job, "IX_CatalogueDeletion_State_ObservedAt_Id", false, ["State", "ObservedAt", "Id"])
    ];

    /// <summary>Reads real PostgreSQL definitions or SQL Server key ordinals without consulting the candidate EF model.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> snapshot)
    {
        var query = provider == Postgres ? MigrationCompatibilityTests.PostgresIndexes : MigrationCompatibilityTests.SqlIndexes;
        var rows = snapshot[query].Select(row => JsonSerializer.Deserialize<string?[]>(row)!)
            .Where(row => row[0] == Schema && Expected.Any(index => index.Table == row[1])).ToArray();
        var nameIndex = provider == Postgres ? PgNameIndex : SqlNameIndex;
        Assert.Equal(Expected.Select(index => index.Name).Order(StringComparer.Ordinal), rows.Select(row => row[nameIndex]).Distinct().Order(StringComparer.Ordinal));
        foreach (var expected in Expected)
        {
            var actual = rows.Where(row => row[nameIndex] == expected.Name).ToArray();
            Assert.All(actual, row => Assert.Equal(expected.Table, row[1]));
            if (provider == Postgres)
            {
                var definition = Assert.Single(actual)[PgDefinitionIndex]!;
                Assert.Equal(expected.Unique, definition.StartsWith(UniqueIndex, StringComparison.Ordinal));
                Assert.EndsWith($"({string.Join(Separator, expected.Columns.Select(column => $"\"{column}\""))})", definition);
            }
            else
            {
                RequireSql(expected, actual, snapshot[DatabaseSnapshot.Columns]);
            }
        }
    }

    /// <summary>Resolves engine column IDs through separately captured information_schema ordinals and rejects included or reordered keys.</summary>
    private static void RequireSql((string Table, string Name, bool Unique, string[] Columns) expected, string?[][] rows, string[] columns)
    {
        var names = columns.Select(row => JsonSerializer.Deserialize<string?[]>(row)!)
            .Where(row => row[0] == Schema && row[1] == expected.Table).ToDictionary(row => row[ColumnOrdinalIndex]!, row => row[ColumnNameIndex]!);
        Assert.All(rows, row =>
        {
            Assert.Equal(expected.Unique ? True : False, row[SqlUniqueIndex]);
            Assert.Equal(False, row[^1]);
        });
        var ordered = rows.OrderBy(row => int.Parse(row[^SqlOrdinalOffset]!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(expected.Columns, ordered.Select(row => names[row[^SqlColumnOffset]!]));
    }

    /// <summary>Calibrates acceptance against a missing indexed target lookup using independently altered captured evidence.</summary>
    internal static void RejectDefects(string provider, Dictionary<string, string[]> snapshot)
    {
        var query = provider == Postgres ? MigrationCompatibilityTests.PostgresIndexes : MigrationCompatibilityTests.SqlIndexes;
        var nameIndex = provider == Postgres ? PgNameIndex : SqlNameIndex;
        var missing = new Dictionary<string, string[]>(snapshot)
        {
            [query] = snapshot[query].Where(row => JsonSerializer.Deserialize<string?[]>(row)![nameIndex] != Expected[^1].Name).ToArray()
        };
        Assert.ThrowsAny<XunitException>(() => Require(provider, missing));
    }
}
