using System.Globalization;
using System.Text.Json;
using IdentityPlatform.Database;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Permits exactly the approved continuation upgrade while preserving frozen interaction rows and complete migration history.</summary>
internal static class IssuerContinuationUpgradeWitness
{
    internal const string ContinuationQuery = "SELECT * FROM \"platform\".\"AdministratorIssuerContinuation\"";
    internal const string InteractionQuery = "SELECT * FROM \"platform\".\"AdministratorInteraction\"";
    private const string Schema = "platform";
    private const string Interaction = "AdministratorInteraction";
    private const string Column = "BrowserIntentDigest";
    private const string Nullable = "YES";
    private const string Length = "64";
    private const string Postgres = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private const string PostgresType = "character varying";
    private const string SqlType = "nvarchar";
    private const string ProductVersion = "10.0.12";
    private const int OriginalSchema = 3;
    private const int OriginalTable = 4;
    private const int ColumnName = 5;
    private const int Ordinal = 6;
    private const int Default = 7;
    private const int NullableIndex = 8;
    private const int Type = 9;
    private const int MaximumLength = 10;
    private static readonly string[] PostgresPlatform = ["20261017224010_AdministratorProtocol", "20261023223630_AdministratorIssuerContinuation"];
    private static readonly string[] SqlPlatform = ["20261017224016_AdministratorProtocol", "20261023223639_AdministratorIssuerContinuation"];

    /// <summary>Recognizes only exact independently queried ownership, never an index/check name that resembles the new column.</summary>
    internal static bool IsInteraction(string row)
    {
        var values = JsonSerializer.Deserialize<string?[]>(row)!;
        return values.Length > 1 && values[0] == Schema && values[1] == Interaction;
    }

    /// <summary>Requires exact nullable bounded storage appended after every old column; callers also require exact columns-query provenance.</summary>
    internal static void RequireColumn(string provider, string row, string[] original)
    {
        var values = JsonSerializer.Deserialize<string?[]>(row)!;
        Assert.True(IsInteraction(row) && values.Length > MaximumLength);
        Assert.Equal(Schema, values[OriginalSchema]);
        Assert.Equal(Interaction, values[OriginalTable]);
        Assert.Equal(Column, values[ColumnName]);
        Assert.Null(values[Default]);
        Assert.Equal(Nullable, values[NullableIndex]);
        Assert.Equal(Length, values[MaximumLength]);
        Assert.Equal(provider switch
        {
            Postgres => PostgresType,
            SqlServer => SqlType,
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        }, values[Type]);
        var previous = original.Where(IsInteraction).Select(value => JsonSerializer.Deserialize<string?[]>(value)!)
            .Max(value => int.Parse(value[Ordinal]!, CultureInfo.InvariantCulture));
        Assert.Equal((previous + 1).ToString(CultureInfo.InvariantCulture), values[Ordinal]);
    }

    /// <summary>Removes exactly one appended null marker only from old interaction rows, retaining every original field and duplicate row.</summary>
    internal static void RequireRows(string[] before, string[] after, int originalColumns)
    {
        var restored = after.Select(row =>
        {
            var values = JsonSerializer.Deserialize<string?[]>(row)!;
            Assert.Equal(originalColumns + 1, values.Length);
            Assert.Null(values[^1]);
            return JsonSerializer.Serialize(values[..^1]);
        }).Order(StringComparer.Ordinal);
        Assert.Equal(DatabaseSnapshot.Fingerprint(before), DatabaseSnapshot.Fingerprint(restored));
    }

    /// <summary>Supplies independently captured frozen history IDs to the reusable same-database conformance runner.</summary>
    internal static string[] HistoryIds(string[] rows) => rows.Select(row => JsonSerializer.Deserialize<string[]>(row)![0])
        .Order(StringComparer.Ordinal).ToArray();

    /// <summary>Accepts only the independently named profile/continuation or Sessions budget additions; original full history rows remain separately compared.</summary>
    internal static void RequireHistory(IRelationalDatabaseProvider provider, DatabaseStore store, IEnumerable<string> additions)
    {
        var expected = (provider.Id, store) switch
        {
            (Postgres, DatabaseStore.Platform) => PostgresPlatform,
            (SqlServer, DatabaseStore.Platform) => SqlPlatform,
            (Postgres, DatabaseStore.Sessions) => BrowserCoordinationUpgradeWitness.PostgresHistory,
            (SqlServer, DatabaseStore.Sessions) => BrowserCoordinationUpgradeWitness.SqlHistory,
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
        Assert.Equal(expected.Select(id => JsonSerializer.Serialize(new[] { id, ProductVersion })).Order(StringComparer.Ordinal), additions.Order(StringComparer.Ordinal));
    }
}
