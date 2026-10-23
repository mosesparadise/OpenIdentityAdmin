using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Seeds forged column provenance and damaged old-row projections so the additive witness cannot discard old data.</summary>
internal static class IssuerContinuationUpgradeWitnessControls
{
    private const int NameIndex = 5;
    private const int OrdinalIndex = 6;
    private const int TypeIndex = 9;
    private const int OldWidth = 3;
    private const string OldName = "ExistingColumn";
    private const string OldOrdinal = "1";
    private const string OtherQuery = "SELECT unrelated_catalog";
    private static readonly (string Provider, string Type)[] Providers = [("PostgreSql", "character varying"), ("SqlServer", "nvarchar")];
    private static readonly string?[] ColumnValues = ["platform", "AdministratorInteraction", "synthetic-catalog", "platform",
        "AdministratorInteraction", "BrowserIntentDigest", "2", null, "YES", "nvarchar", "64"];
    private static readonly (int Index, string? Value)[] InvalidColumns = [(0, "bff"), (1, "OtherTable"), (3, "bff"),
        (4, "OtherTable"), (5, "browserintentdigest"), (6, "1"), (6, "3"), (7, "forged-default"), (8, "NO"), (9, "text"), (10, "65")];
    private static readonly string[] Before = ["[\"id\",\"user\",\"pending\"]"];
    private static readonly string[] After = ["[\"id\",\"user\",\"pending\",null]"];
    private static readonly string[] InvalidRows = ["[\"id\",\"user\",\"pending\",\"bound\"]", "[\"id\",\"other\",\"pending\",null]",
        "[\"id\",\"user\",\"pending\",null,null]", "[\"id\",\"user\",null]"];

    /// <summary>Checks correct controls and independent failures before any actual provider compatibility execution.</summary>
    internal static void Verify(string provider)
    {
        RequireColumns(provider, Providers.Single(value => value.Provider == provider).Type);
        IssuerContinuationUpgradeWitness.RequireRows(Before, After, OldWidth);
        foreach (var row in InvalidRows)
        {
            Assert.ThrowsAny<XunitException>(() => IssuerContinuationUpgradeWitness.RequireRows(Before, [row], OldWidth));
        }
        Assert.ThrowsAny<XunitException>(() => IssuerContinuationUpgradeWitness.RequireRows(Before, [], OldWidth));
        Assert.ThrowsAny<XunitException>(() => IssuerContinuationUpgradeWitness.RequireRows(Before, [After[0], After[0]], OldWidth));
    }

    /// <summary>Requires the exact column query and rejects duplicate additions, wrong owners and every bounded-column attribute substitution.</summary>
    private static void RequireColumns(string provider, string type)
    {
        var column = (string?[])ColumnValues.Clone();
        column[TypeIndex] = type;
        var old = (string?[])column.Clone();
        old[NameIndex] = OldName;
        old[OrdinalIndex] = OldOrdinal;
        string[] before = [JsonSerializer.Serialize(old)];
        var current = JsonSerializer.Serialize(column);
        AdministratorUpgradeWitness.RequireCatalog(before, [before[0], current], DatabaseSnapshot.Columns, provider);
        Assert.ThrowsAny<XunitException>(() => AdministratorUpgradeWitness.RequireCatalog(before, [before[0], current], OtherQuery, provider));
        Assert.ThrowsAny<XunitException>(() => AdministratorUpgradeWitness.RequireCatalog(before, [before[0], current, current], DatabaseSnapshot.Columns, provider));
        Assert.ThrowsAny<XunitException>(() => AdministratorUpgradeWitness.RequireCatalog(before, before, DatabaseSnapshot.Columns, provider));
        foreach (var (index, value) in InvalidColumns)
        {
            var forged = (string?[])column.Clone();
            forged[index] = value;
            Assert.ThrowsAny<XunitException>(() => AdministratorUpgradeWitness.RequireCatalog(before,
                [before[0], JsonSerializer.Serialize(forged)], DatabaseSnapshot.Columns, provider));
        }
    }
}
