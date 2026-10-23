using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Fingerprints PostgreSQL/SQL Server schema metadata and every row in both logical stores, including complete history rows.</summary>
internal static class DatabaseSnapshot
{
    private const string Tables = "SELECT table_schema, table_name FROM information_schema.tables WHERE table_schema IN ('platform', 'bff') AND table_type = 'BASE TABLE' ORDER BY table_schema, table_name";
    internal const string Columns = "SELECT table_schema, table_name, c.* FROM information_schema.columns c WHERE table_schema IN ('platform', 'bff')";
    private const string Constraints = "SELECT table_schema, table_name, c.* FROM information_schema.table_constraints c WHERE table_schema IN ('platform', 'bff')";
    private const string Keys = "SELECT table_schema, table_name, c.* FROM information_schema.key_column_usage c WHERE table_schema IN ('platform', 'bff')";
    private const string References = "SELECT t.table_schema, t.table_name, r.* FROM information_schema.referential_constraints r LEFT JOIN information_schema.table_constraints t ON t.constraint_catalog=r.constraint_catalog AND t.constraint_schema=r.constraint_schema AND t.constraint_name=r.constraint_name WHERE r.constraint_schema IN ('platform', 'bff')";
    private static readonly CompositeFormat Rows = CompositeFormat.Parse("SELECT * FROM \"{0}\".\"{1}\"");
    private const int CommandSeconds = 30;
    private const string RoundTripDate = "O";
    private const string IdentifierQuote = "\"";
    private const string EscapedIdentifierQuote = "\"\"";

    /// <summary>Captures full catalog shape and sorted table contents without logging identity or database material.</summary>
    internal static async Task<string> CaptureAsync(DbConnection connection, string indexQuery, string checkQuery, CancellationToken token)
    {
        var snapshot = await CaptureRowsAsync(connection, indexQuery, checkQuery, token);
        return Fingerprint(snapshot.SelectMany(pair => new[] { pair.Key }.Concat(pair.Value)));
    }

    /// <summary>Retains separately bound catalog/table rows for an independent additive-upgrade witness; values are never logged.</summary>
    internal static async Task<Dictionary<string, string[]>> CaptureRowsAsync(DbConnection connection, string indexQuery, string checkQuery, CancellationToken token)
    {
        await connection.OpenAsync(token);
        try
        {
            var evidence = new Dictionary<string, string[]>();
            foreach (var query in new[] { Columns, Constraints, Keys, checkQuery, References, indexQuery })
            {
                evidence.Add(query, await ReadAsync(connection, query, token));
            }
            var tables = await TableNamesAsync(connection, token);
            Assert.NotEmpty(tables);
            foreach (var (schema, table) in tables)
            {
                var query = string.Format(CultureInfo.InvariantCulture, Rows, Escape(schema), Escape(table));
                evidence.Add(query, await ReadAsync(connection, query, token));
            }
            return evidence;
        }
        finally { await connection.CloseAsync(); }
    }

    /// <summary>Compares complete serialized evidence without exposing fixture rows in assertion output.</summary>
    internal static string Fingerprint(IEnumerable<string> rows) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', rows))));

    /// <summary>Enumerates only real product-schema tables rather than inferring storage from EF metadata.</summary>
    private static async Task<List<(string Schema, string Table)>> TableNamesAsync(DbConnection connection, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Tables;
        command.CommandTimeout = CommandSeconds;
        var tables = new List<(string, string)>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            tables.Add((reader.GetString(0), reader.GetString(1)));
        }
        return tables;
    }

    /// <summary>Serializes typed values with lengths and sorts rows so physical storage order cannot change evidence.</summary>
    private static async Task<string[]> ReadAsync(DbConnection connection, string query, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.CommandTimeout = CommandSeconds;
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(System.Text.Json.JsonSerializer.Serialize(values.Select(Value)));
        }
        return rows.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Retains binary bytes, precise dates and null distinctions with culture-independent conversion.</summary>
    private static string? Value(object value) => value switch
    {
        DBNull => null,
        byte[] bytes => Convert.ToBase64String(bytes),
        DateTime date => date.ToString(RoundTripDate, CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString(RoundTripDate, CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };

    /// <summary>Escapes discovered identifiers instead of treating database metadata as executable text.</summary>
    private static string Escape(string identifier) => identifier.Replace(IdentifierQuote, EscapedIdentifierQuote, StringComparison.Ordinal);
}
