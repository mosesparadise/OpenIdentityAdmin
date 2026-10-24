using System.Text.Json;
using IdentityPlatform.Database;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Executes nonvacuous negative controls for exact browser schema/history additions without requiring database startup.</summary>
public sealed class BrowserCoordinationUpgradeWitnessTests
{
    private const string Postgres = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private const string ProductVersion = "10.0.12";
    private const string ForeignSchema = "platform";
    private const string ContextTable = "BrowserContext";
    private const string UnexpectedRow = "unexpected browser authority";

    private static readonly string[] ChangedBudget = ["1", "1"];
    private static readonly string[] Budget = ["1", "0"];

    /// <summary>Missing/extra history, foreign table ownership and any seeded browser authority cannot pass the unchanged-data upgrade witness.</summary>
    [Theory]
    [InlineData(Postgres, Traits = ["Provider", Postgres])]
    [InlineData(SqlServer, Traits = ["Provider", SqlServer])]
    public void Given_ApprovedBrowserAdditions_When_HistoryOwnershipOrSeedIsForged_Then_IndependentWitnessRejects(string provider)
    {
        var registration = TestDatabaseProviders.Resolve(provider);
        var history = (provider == Postgres ? BrowserCoordinationUpgradeWitness.PostgresHistory : BrowserCoordinationUpgradeWitness.SqlHistory)
            .Select(id => JsonSerializer.Serialize(new[] { id, ProductVersion })).ToArray();
        IssuerContinuationUpgradeWitness.RequireHistory(registration, DatabaseStore.Sessions, history);
        Assert.NotNull(Record.Exception(() => IssuerContinuationUpgradeWitness.RequireHistory(registration, DatabaseStore.Sessions, history[..^1])));
        Assert.NotNull(Record.Exception(() => IssuerContinuationUpgradeWitness.RequireHistory(registration, DatabaseStore.Sessions, [.. history, history[0]])));
        Assert.NotNull(Record.Exception(() => AdministratorUpgradeWitness.RequireCatalog([], [JsonSerializer.Serialize(new[] { ForeignSchema, ContextTable })])));
        var snapshot = BrowserCoordinationUpgradeWitness.Queries.ToDictionary(query => query, _ => Array.Empty<string>(), StringComparer.Ordinal);
        snapshot[BrowserCoordinationUpgradeWitness.Queries[^1]] = [JsonSerializer.Serialize(Budget)];
        BrowserCoordinationUpgradeWitness.RequireSeeds(snapshot);
        snapshot[BrowserCoordinationUpgradeWitness.Queries[0]] = [UnexpectedRow];
        Assert.NotNull(Record.Exception(() => BrowserCoordinationUpgradeWitness.RequireSeeds(snapshot)));
        snapshot[BrowserCoordinationUpgradeWitness.Queries[0]] = [];
        snapshot[BrowserCoordinationUpgradeWitness.Queries[^1]] = [JsonSerializer.Serialize(ChangedBudget)];
        Assert.NotNull(Record.Exception(() => BrowserCoordinationUpgradeWitness.RequireSeeds(snapshot)));
    }
}
