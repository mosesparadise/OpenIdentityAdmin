using System.Globalization;
using System.Text.Json;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Allows only the explicit nullable audit relation appended to the frozen Platform audit table.</summary>
internal static class BrowserAuditUpgradeWitness
{
    private const string Postgres = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private const string PostgresType = "uuid";
    private const string SqlType = "uniqueidentifier";
    internal const string Query = "SELECT * FROM \"platform\".\"SecurityAuditIntent\"";
    private const string Schema = "platform";
    private const string Table = "SecurityAuditIntent";
    private const string Column = "RelatedCorrelationId";
    private const string Nullable = "YES";
    private const int OriginalSchema = 3;
    private const int OriginalTable = 4;
    private const int Name = 5;
    private const int Ordinal = 6;
    private const int Default = 7;
    private const int NullableIndex = 8;
    private const int Type = 9;
    private const int Length = 10;

    /// <summary>Recognizes only independently queried Platform audit ownership, not a matching index or constraint name.</summary>
    internal static bool IsAudit(string row)
    {
        var values = JsonSerializer.Deserialize<string?[]>(row)!;
        return values.Length > 1 && values[0] == Schema && values[1] == Table;
    }

    /// <summary>Requires one GUID column with no default or invented historical value after all old audit columns.</summary>
    internal static void RemoveColumn(List<string> additions, string[] before, string provider)
    {
        var row = Assert.Single(additions, IsAudit);
        var values = JsonSerializer.Deserialize<string?[]>(row)!;
        Assert.True(values.Length > Length);
        Assert.Equal(Schema, values[OriginalSchema]);
        Assert.Equal(Table, values[OriginalTable]);
        Assert.Equal(Column, values[Name]);
        Assert.Null(values[Default]);
        Assert.Equal(Nullable, values[NullableIndex]);
        Assert.Null(values[Length]);
        Assert.Equal(provider switch
        {
            Postgres => PostgresType,
            SqlServer => SqlType,
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        }, values[Type]);
        var previous = before.Where(IsAudit).Select(value => JsonSerializer.Deserialize<string?[]>(value)!)
            .Max(value => int.Parse(value[Ordinal]!, CultureInfo.InvariantCulture));
        Assert.Equal((previous + 1).ToString(CultureInfo.InvariantCulture), values[Ordinal]);
        Assert.True(additions.Remove(row));
    }
}
