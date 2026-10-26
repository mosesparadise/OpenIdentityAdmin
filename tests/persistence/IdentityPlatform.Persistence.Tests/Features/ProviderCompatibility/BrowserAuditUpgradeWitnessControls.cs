using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Seeds forged ownership, schema and old-row changes independently of the migration implementation.</summary>
internal static class BrowserAuditUpgradeWitnessControls
{
    private const string Postgres = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private const string PostgresType = "uuid";
    private const string SqlType = "uniqueidentifier";
    private const int TypeIndex = 9;
    private const int OldWidth = 3;
    private static readonly string?[] Column = ["platform", "SecurityAuditIntent", "synthetic-catalog", "platform",
        "SecurityAuditIntent", "RelatedCorrelationId", "2", null, "YES", "uuid", null];
    internal static readonly string Original = JsonSerializer.Serialize<string?[]>(["platform", "SecurityAuditIntent", "synthetic-catalog",
        "platform", "SecurityAuditIntent", "ExistingColumn", "1", null, "NO", "uuid", null]);
    private static readonly (int Index, string? Value)[] Faults = [(0, "bff"), (1, "OtherTable"), (3, "bff"), (4, "OtherTable"),
        (5, "relatedcorrelationid"), (6, "1"), (6, "3"), (7, "generated-default"), (8, "NO"), (9, "text"), (10, "32")];
    private static readonly string[] Before = ["[\"id\",\"actor\",\"accepted\"]"];
    private static readonly string[] After = ["[\"id\",\"actor\",\"accepted\",null]"];
    private static readonly string[] InvalidRows = ["[\"id\",\"actor\",\"accepted\",\"\"]", "[\"id\",\"actor\",\"accepted\",\"invented-relation\"]",
        "[\"id\",\"changed\",\"accepted\",null]", "[\"id\",\"actor\",\"accepted\",null,null]", "[\"id\",\"actor\",null]"];

    /// <summary>Supplies independently expected native GUID metadata for existing integrated witness controls.</summary>
    internal static string Added(string provider)
    {
        var column = (string?[])Column.Clone();
        column[TypeIndex] = provider switch
        {
            Postgres => PostgresType,
            SqlServer => SqlType,
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
        return JsonSerializer.Serialize(column);
    }

    /// <summary>Requires positive controls and decisive rejection of missing/duplicate columns, changed types and fabricated historical relations.</summary>
    internal static void Verify(string provider)
    {
        var current = Added(provider);
        var valid = new List<string> { current };
        BrowserAuditUpgradeWitness.RemoveColumn(valid, [Original], provider);
        Assert.Empty(valid);
        Assert.ThrowsAny<XunitException>(() => BrowserAuditUpgradeWitness.RemoveColumn([], [Original], provider));
        Assert.ThrowsAny<XunitException>(() => BrowserAuditUpgradeWitness.RemoveColumn([current, current], [Original], provider));
        foreach (var (index, value) in Faults)
        {
            var forged = JsonSerializer.Deserialize<string?[]>(current)!;
            forged[index] = value;
            Assert.ThrowsAny<XunitException>(() => BrowserAuditUpgradeWitness.RemoveColumn([JsonSerializer.Serialize(forged)], [Original], provider));
        }
        IssuerContinuationUpgradeWitness.RequireRows(Before, After, OldWidth);
        foreach (var row in InvalidRows)
        {
            Assert.ThrowsAny<XunitException>(() => IssuerContinuationUpgradeWitness.RequireRows(Before, [row], OldWidth));
        }
        Assert.ThrowsAny<XunitException>(() => IssuerContinuationUpgradeWitness.RequireRows(Before, [], OldWidth));
        Assert.ThrowsAny<XunitException>(() => IssuerContinuationUpgradeWitness.RequireRows(Before, [After[0], After[0]], OldWidth));
    }
}
