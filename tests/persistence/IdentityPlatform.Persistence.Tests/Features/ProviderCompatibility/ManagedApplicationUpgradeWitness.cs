using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Accepts only two independently named new tables and one Platform history row while comparing every old physical value and metadata row.</summary>
internal static class ManagedApplicationUpgradeWitness
{
    internal const string PostgresMigration = "20261030100418_ManagedApplications";
    internal const string SqlMigration = "20261030100520_ManagedApplications";
    internal const string PolicyQuery = "SELECT * FROM \"platform\".\"ApplicationPolicy\"";
    internal const string InteractionQuery = "SELECT * FROM \"platform\".\"ManagedLoginInteraction\"";
    internal static readonly (string Schema, string Table)[] Tables = [("platform", "ApplicationPolicy"), ("platform", "ManagedLoginInteraction")];
    internal static readonly string[] Queries = [PolicyQuery, InteractionQuery];
    private const string Postgres = "PostgreSql";
    private const string ProductVersion = "10.0.14";
    private const string TablePrefix = "SELECT * FROM \"";

    /// <summary>Rejects old-row damage, undocumented schema ownership, extra history and nonempty automatic policy adoption.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        Assert.Equal(Queries.Order(StringComparer.Ordinal), after.Keys.Except(before.Keys).Order(StringComparer.Ordinal));
        Assert.Empty(before.Keys.Except(after.Keys));
        foreach (var (query, rows) in before)
        {
            if (query == AdministratorUpgradeWitness.PlatformHistory)
            {
                var addition = provider == Postgres ? PostgresMigration : SqlMigration;
                var expected = rows.Append(JsonSerializer.Serialize(new[] { addition, ProductVersion })).Order(StringComparer.Ordinal);
                Assert.Equal(DatabaseSnapshot.Fingerprint(expected), DatabaseSnapshot.Fingerprint(after[query]));
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
                Assert.All(additions, row => Assert.Contains(Tables, owner => IsOwned(row, owner)));
            }
        }
        RequireEmpty(after);
        ManagedSchemaColumns.Require(provider, after[DatabaseSnapshot.Columns]);
    }

    /// <summary>Proves upgrades do not silently adopt native registrations or seed managed authentication authority.</summary>
    internal static void RequireEmpty(Dictionary<string, string[]> snapshot)
    {
        foreach (var query in Queries)
        {
            Assert.Empty(snapshot[query]);
        }
    }

    /// <summary>Uses the snapshot's actual schema/table ownership prefix rather than accepting an object-name resemblance.</summary>
    private static bool IsOwned(string row, (string Schema, string Table) owner)
    {
        var values = JsonSerializer.Deserialize<string?[]>(row)!;
        return values.Length > 1 && values[0] == owner.Schema && values[1] == owner.Table;
    }

    /// <summary>Seeds missing/extra table, automatic adoption, altered history and old-catalog damage against the actual successful upgrade witness.</summary>
    internal static void RejectDefects(string provider, Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        const string Unexpected = "SELECT * FROM \"platform\".\"UnexpectedManagedState\"";
        var missing = new Dictionary<string, string[]>(after, StringComparer.Ordinal);
        missing.Remove(PolicyQuery);
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, missing));
        var extra = new Dictionary<string, string[]>(after, StringComparer.Ordinal) { [Unexpected] = [] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, extra));
        var adopted = new Dictionary<string, string[]>(after, StringComparer.Ordinal) { [PolicyQuery] = [string.Empty] };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, adopted));
        var history = new Dictionary<string, string[]>(after, StringComparer.Ordinal)
        {
            [AdministratorUpgradeWitness.PlatformHistory] = before[AdministratorUpgradeWitness.PlatformHistory]
        };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, history));
        var removed = before[DatabaseSnapshot.Columns][0];
        var damaged = new Dictionary<string, string[]>(after, StringComparer.Ordinal)
        {
            [DatabaseSnapshot.Columns] = after[DatabaseSnapshot.Columns].Where(row => row != removed).ToArray()
        };
        Assert.ThrowsAny<XunitException>(() => Require(provider, before, damaged));
    }
}
