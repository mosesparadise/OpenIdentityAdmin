using System.Text.Json;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Permits only the five exact dormant Sessions additions and their one inert budget seed against the unchanged frozen producer.</summary>
internal static class BrowserCoordinationUpgradeWitness
{
    internal static readonly (string Schema, string Table)[] Tables = [("bff", "BrowserContext"), ("bff", "BrowserLoginIntent"),
        ("bff", "BrowserLogoutContinuation"), ("bff", "BrowserSessionAudit"), ("bff", "BrowserSessionBudget")];
    internal static readonly string[] Queries = ["SELECT * FROM \"bff\".\"BrowserContext\"", "SELECT * FROM \"bff\".\"BrowserLoginIntent\"",
        "SELECT * FROM \"bff\".\"BrowserLogoutContinuation\"", "SELECT * FROM \"bff\".\"BrowserSessionAudit\"", "SELECT * FROM \"bff\".\"BrowserSessionBudget\""];
    internal static readonly string[] PostgresHistory = ["20261017224013_AdministratorClientStateBudget", "20261024000243_BrowserSessionCoordination"];
    internal static readonly string[] SqlHistory = ["20261017224019_AdministratorClientStateBudget", "20261024000342_BrowserSessionCoordination"];
    private static readonly string[] Budget = ["1", "0"];

    /// <summary>Rejects any migrated browser authority, unexpected audit, missing table or nonzero quota seed.</summary>
    internal static void RequireSeeds(Dictionary<string, string[]> snapshot)
    {
        foreach (var query in Queries[..^1])
        {
            Assert.Empty(snapshot[query]);
        }
        Assert.Equal(JsonSerializer.Serialize(Budget), Assert.Single(snapshot[Queries[^1]]));
    }
}
