using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Compares all predecessor physical rows and metadata while allowing only the independently named lifecycle additions.</summary>
internal static class LifecycleUpgradeWitness
{
    private const string Postgres = "PostgreSql";
    private const string PostgresMigration = "20261105000917_ApplicationLifecycle";
    private const string SqlMigration = "20261105000920_ApplicationLifecycle";
    private const string EnabledValue = "True";
    private const string TablePrefix = "SELECT * FROM \"";
    private const string Schema = "platform";
    private const string NotNullable = "NO";
    private const string PostgresBoolean = "boolean";
    private const string SqlBoolean = "bit";
    private const string PostgresDefault = "true";
    private const string SqlDefault = "(CONVERT([bit],(1)))";
    private const string AuditId = "AuditId";
    private const string ReasonCode = "ReasonCode";
    private const string PostgresGuid = "uuid";
    private const string SqlGuid = "uniqueidentifier";
    private const string PostgresInteger = "integer";
    private const string SqlInteger = "int";
    private const string UnexpectedRow = "unexpected";
    private const string FalseDefault = "false";
    private const string Constraints = "SELECT table_schema, table_name, c.* FROM information_schema.table_constraints c WHERE table_schema IN ('platform', 'bff')";
    private const string EnabledNotNull = "ApplicationPolicy_Enabled_not_null";
    private const string Check = "CHECK";
    private const int ConstraintName = 4;
    private const int ConstraintType = 8;

    private const string AuditQuery = "SELECT * FROM \"platform\".\"ApplicationLifecycleAuditDetail\"";
    private const string PolicyTable = "ApplicationPolicy";
    private const string AuditTable = "ApplicationLifecycleAuditDetail";
    private const string Enabled = "Enabled";
    private const int ColumnName = 5;
    private const int ColumnDefault = 7;
    private const int Nullable = 8;
    private const int DataType = 9;

    /// <summary>Requires exact history, unchanged credentials/grants/interactions/audits, and an enabled default for every existing policy.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        Assert.Equal([AuditQuery], after.Keys.Except(before.Keys));
        Assert.Empty(before.Keys.Except(after.Keys));
        Assert.Empty(after[AuditQuery]);
        foreach (var (query, rows) in before)
        {
            if (query == AdministratorUpgradeWitness.PlatformHistory)
            {
                BrowserAuditMigrationRows.RequireHistory(rows, after[query], provider == Postgres ?
                    PostgresMigration : SqlMigration);
            }
            else if (query == ManagedApplicationUpgradeWitness.PolicyQuery)
            {
                var expected = rows.Select(row => JsonSerializer.Serialize(JsonSerializer.Deserialize<string?[]>(row)!.Append(EnabledValue))).Order(StringComparer.Ordinal);
                Assert.Equal(DatabaseSnapshot.Fingerprint(expected), DatabaseSnapshot.Fingerprint(after[query]));
            }
            else if (query.StartsWith(TablePrefix, StringComparison.Ordinal))
            {
                Assert.Equal(DatabaseSnapshot.Fingerprint(rows), DatabaseSnapshot.Fingerprint(after[query]));
            }
            else
            {
                var additions = after[query].ToList();
                Assert.All(rows, row => Assert.True(additions.Remove(row)));
                Assert.All(additions, row => RequireOwner(query, row));
            }
        }
        RequireColumns(provider, after[DatabaseSnapshot.Columns]);
    }

    /// <summary>Refuses unrelated metadata additions and permits exactly the new policy column outside the new audit-detail table.</summary>
    private static void RequireOwner(string query, string row)
    {
        var values = JsonSerializer.Deserialize<string?[]>(row)!;
        Assert.Equal(Schema, values[0]);
        Assert.True(values[1] == AuditTable || values[1] == PolicyTable &&
            (query == DatabaseSnapshot.Columns && values[ColumnName] == Enabled ||
             query == Constraints && values[ConstraintName] == EnabledNotNull && values[ConstraintType] == Check ||
             query == MigrationCompatibilityTests.PostgresChecks && values[ConstraintName] == EnabledNotNull));
    }

    /// <summary>Checks physical type, non-nullability, true default and the complete two-column audit detail on each engine.</summary>
    private static void RequireColumns(string provider, string[] rows)
    {
        var columns = rows.Select(row => JsonSerializer.Deserialize<string?[]>(row)!).ToArray();
        var enabled = Assert.Single(columns, row => row[0] == Schema && row[1] == PolicyTable && row[ColumnName] == Enabled);
        Assert.Equal(NotNullable, enabled[Nullable]);
        Assert.Equal(provider == Postgres ? PostgresBoolean : SqlBoolean, enabled[DataType]);
        Assert.Equal(provider == Postgres ? PostgresDefault : SqlDefault, enabled[ColumnDefault]);
        var detail = columns.Where(row => row[0] == Schema && row[1] == AuditTable).OrderBy(row => row[ColumnName]).ToArray();
        Assert.Equal([AuditId, ReasonCode], detail.Select(row => row[ColumnName]));
        Assert.All(detail, row => Assert.Equal(NotNullable, row[Nullable]));
        Assert.Equal(provider == Postgres ? PostgresGuid : SqlGuid, detail[0][DataType]);
        Assert.Equal(provider == Postgres ? PostgresInteger : SqlInteger, detail[1][DataType]);
    }

    /// <summary>Proves the successful witness rejects lost history, damaged legacy data, false defaults and unexpected adoption.</summary>
    internal static void RejectDefects(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        foreach (var key in new[] { AdministratorUpgradeWitness.PlatformHistory, ManagedApplicationUpgradeWitness.PolicyQuery })
        {
            var damaged = new Dictionary<string, string[]>(after) { [key] = before[key] };
            Assert.ThrowsAny<XunitException>(() => Require(provider, before, damaged));
        }
        var adopted = new Dictionary<string, string[]>(after) { [AuditQuery] = [UnexpectedRow] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, adopted));
        var columns = after[DatabaseSnapshot.Columns].Select(row =>
        {
            var values = JsonSerializer.Deserialize<string?[]>(row)!;
            if (values[1] == PolicyTable && values[ColumnName] == Enabled)
            {
                values[ColumnDefault] = FalseDefault;
            }
            return JsonSerializer.Serialize(values);
        }).ToArray();
        var wrongDefault = new Dictionary<string, string[]>(after) { [DatabaseSnapshot.Columns] = columns };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, wrongDefault));
    }
}
