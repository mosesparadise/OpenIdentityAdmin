using System.Text.Json;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Checks the original provider constraint expressions and preserves every previous row through the public verification upgrade.</summary>
internal static class DeviceHumanSchemaWitness
{
    private const string Pg = "PostgreSql";
    internal const string PgMigration = "20261128100434_ManagedDeviceHumanInteraction";
    internal const string SqlMigration = "20261128100437_ManagedDeviceHumanInteraction";
    private const string Human = "ManagedLoginInteraction";
    private const string Window = "DevicePollingWindow";
    private const string Device = "DeviceEntryId";
    private const string Attempt = "LastCleanupAttempt";
    private const string Grant = "NativeAuthorizationId";
    private const string Index = "IX_ManagedLoginInteraction_DeviceEntryId";
    private const string Resource = "DeviceHumanChecks";
    private const string Platform = "platform";
    private const string TablePrefix = "SELECT * FROM \"platform\".\"";
    private const int ColumnName = 5;
    private const int Catalog = 2;

    /// <summary>Requires the exact three nullable physical columns, new unique index, four amended constraints and migration history entry.</summary>
    internal static Dictionary<string, string[]> Predecessor(string provider, Dictionary<string, string[]> source)
    {
        var postgres = provider == Pg;
        var migration = postgres ? PgMigration : SqlMigration;
        var history = AdministratorUpgradeWitness.PlatformHistory;
        if (!source[history].Any(row => Parts(row)[0] == migration))
        {
            return source;
        }
        var result = new Dictionary<string, string[]>(source, StringComparer.Ordinal);
        result[history] = source[history].Where(row => Parts(row)[0] != migration).ToArray();
        BrowserAuditMigrationRows.RequireHistory(result[history], source[history], migration);
        var columns = source[DatabaseSnapshot.Columns];
        var human = columns.Single(row => Parts(row) is [Platform, Human, ..] cells && cells[ColumnName] == Device);
        var window = columns.Single(row => Parts(row) is [Platform, Window, ..] cells && cells[ColumnName] == Grant);
        var cleanup = columns.Single(row => Parts(row) is [Platform, Window, ..] cells && cells[ColumnName] == Attempt);
        result[DatabaseSnapshot.Columns] = columns.Except([human, window, cleanup]).ToArray();
        var indexes = postgres ? MigrationCompatibilityTests.PostgresIndexes : MigrationCompatibilityTests.SqlIndexes;
        var index = source[indexes].Single(row => Parts(row) is [Platform, Human, ..] cells && cells.Contains(Index, StringComparer.Ordinal));
        result[indexes] = source[indexes].Where(row => row != index).ToArray();
        using var resource = typeof(DeviceHumanSchemaWitness).Assembly.GetManifestResourceStream(Resource)!;
        var checks = postgres ? MigrationCompatibilityTests.PostgresChecks : MigrationCompatibilityTests.SqlChecks;
        var delta = JsonSerializer.Deserialize<Dictionary<string, ProfileStorageWitness.Change>>(resource)![provider];
        result[checks] = ProfileStorageWitness.Restore(source[checks], delta, Parts(human)[Catalog]!);
        foreach (var table in new[] { Human, Window })
        {
            var query = TablePrefix + table + '"';
            result[query] = source[query].Select(RemoveNull).Select(row => table == Window ? RemoveNull(row) : row).Order(StringComparer.Ordinal).ToArray();
        }
        return result;
    }

    /// <summary>Requires the migrated value to be null before comparing all existing data, including retained decisions.</summary>
    private static string RemoveNull(string row)
    {
        var cells = Parts(row);
        Assert.Null(cells[^1]);
        return JsonSerializer.Serialize(cells[..^1]);
    }

    /// <summary>Parses one captured provider metadata or data row without exposing its values.</summary>
    private static string?[] Parts(string row) => JsonSerializer.Deserialize<string?[]>(row)!;
}
