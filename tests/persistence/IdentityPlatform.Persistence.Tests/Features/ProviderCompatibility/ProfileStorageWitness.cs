using System.Text.Json;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Verifies the exact provider-specific metadata changes before projecting a snapshot back to the retirement schema.</summary>
internal static class ProfileStorageWitness
{
    internal const string PostgresMigration = "20261121010753_ManagedClientProfiles";
    internal const string SqlMigration = "20261121010800_ManagedClientProfiles";
    private const string Resource = "ManagedClientProfileSchema";
    private const string PostgreSql = "PostgreSql";
    private const string CatalogPlaceholder = "$catalog";
    private const int CatalogIndex = 2;
    private const string Platform = "platform";
    private const string Policy = "ApplicationPolicy";
    private const string Columns = "columns";
    private const string Constraints = "table_constraints";
    private const string Checks = "check_constraints";
    private sealed record Change(string?[][] Old, string?[][] New);

    /// <summary>Restores only independently captured metadata rows and the single added migration, preserving all other physical observations.</summary>
    internal static Dictionary<string, string[]> Predecessor(string provider, Dictionary<string, string[]> after)
    {
        after = BrowserOriginUpgradeTests.Predecessor(provider, after);
        using var stream = typeof(ProfileStorageWitness).Assembly.GetManifestResourceStream(Resource) ?? throw new InvalidDataException();
        var changes = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Change>>>(stream)![provider];
        var postgres = provider == PostgreSql;
        var catalog = after[DatabaseSnapshot.Columns].Select(row => JsonSerializer.Deserialize<string?[]>(row)!)
            .First(row => row[0] == Platform && row[1] == Policy)[CatalogIndex]!;
        var result = new Dictionary<string, string[]>(after, StringComparer.Ordinal);
        foreach (var (name, change) in changes)
        {
            var query = name switch
            {
                Columns => DatabaseSnapshot.Columns,
                Constraints => DatabaseSnapshot.Constraints,
                Checks => postgres ? MigrationCompatibilityTests.PostgresChecks : MigrationCompatibilityTests.SqlChecks,
                _ => throw new InvalidDataException()
            };
            result[query] = Restore(after[query], change, catalog);
        }
        var migration = postgres ? PostgresMigration : SqlMigration;
        var history = AdministratorUpgradeWitness.PlatformHistory;
        result[history] = after[history].Where(row => JsonSerializer.Deserialize<string[]>(row)![0] != migration).ToArray();
        BrowserAuditMigrationRows.RequireHistory(result[history], after[history], migration);
        return result;
    }

    /// <summary>Requires each expected current row exactly once and preserves any extra row for the full predecessor fingerprint comparison.</summary>
    private static string[] Restore(string[] rows, Change change, string catalog)
    {
        var result = rows.ToList();
        foreach (var current in change.New)
        {
            var bound = Bind(current, catalog);
            Assert.True(result.Remove(bound));
            Assert.DoesNotContain(bound, result);
        }
        foreach (var old in change.Old)
        {
            var bound = Bind(old, catalog);
            Assert.DoesNotContain(bound, result);
            result.Add(bound);
        }
        return result.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Substitutes the current database name in catalog cells only, leaving all physical values and expressions intact.</summary>
    private static string Bind(string?[] cells, string catalog) =>
        JsonSerializer.Serialize(cells.Select(value => value == CatalogPlaceholder ? catalog : value));
}
