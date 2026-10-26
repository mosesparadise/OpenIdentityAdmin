using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Captures every physical audit field before and after upgrade without reading through the candidate EF entity shape.</summary>
internal static class BrowserAuditMigrationRows
{
    private const string PlatformQuery = "SELECT * FROM \"platform\".\"SecurityAuditIntent\" ORDER BY \"Id\"";
    private const string SessionsQuery = "SELECT * FROM \"bff\".\"BrowserSessionAudit\" ORDER BY \"Id\"";
    private const string PlatformInsert = "INSERT INTO \"platform\".\"SecurityAuditIntent\" (\"Id\", \"Event\", \"ActorType\", \"ActorId\", \"TargetType\", \"TargetId\", \"Outcome\", \"CorrelationId\", \"OccurredAt\") VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})";
    private const string SessionsInsert = "INSERT INTO \"bff\".\"BrowserSessionAudit\" (\"Id\", \"CorrelationId\", \"Generation\", \"OccurredAt\", \"Operation\", \"Subject\", \"SourceSession\") VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})";
    private const string Event = "synthetic-previous-audit";
    private const string Actor = "account";
    private const string Subject = "synthetic-existing-account";
    private const string Source = "synthetic-existing-source";
    private const string Outcome = "accepted";
    private const string Target = "administrator-account";
    private const string Relation = "RelatedCorrelationId";
    private const long Occurred = 100;
    private const int RowCount = 2;
    private const int CommandSeconds = 30;
    private const string PlatformHistory = "SELECT * FROM \"platform\".\"PlatformMigrations\" ORDER BY \"MigrationId\"";
    private const string SessionsHistory = "SELECT * FROM \"bff\".\"SessionMigrations\" ORDER BY \"MigrationId\"";
    private const string ProductVersion = "10.0.12";
    private const string ActorParameter = "legacyActor";
    private const string TargetParameter = "legacyTarget";
    private const string SubjectParameter = "legacySubject";
    private const string SourceParameter = "legacySource";

    /// <summary>Populates only existing columns using parameters, with both absent and authenticated provenance represented.</summary>
    internal static async Task SeedAsync(DbContext platform, DbContext sessions, CancellationToken token)
    {
        foreach (var authenticated in new[] { false, true })
        {
            await platform.Database.ExecuteSqlRawAsync(PlatformInsert,
                [Guid.NewGuid(), Event, Actor, authenticated ? Subject : NullText(platform, ActorParameter), Target,
                    authenticated ? Subject : NullText(platform, TargetParameter), Outcome, Guid.NewGuid(), Occurred], token);
            await sessions.Database.ExecuteSqlRawAsync(SessionsInsert,
                [Guid.NewGuid(), Guid.NewGuid(), 1L, Occurred, 1, authenticated ? Subject : NullText(sessions, SubjectParameter),
                    authenticated ? Source : NullText(sessions, SourceParameter)], token);
        }
    }

    /// <summary>Supplies native nullable text parameters so EF never infers a store mapping for the DBNull CLR type.</summary>
    private static DbParameter NullText(DbContext database, string name)
    {
        using var command = database.Database.GetDbConnection().CreateCommand();
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = DBNull.Value;
        return parameter;
    }

    /// <summary>Reads old widths and every value independently from each actual relational table.</summary>
    internal static async Task<string[][]> CaptureAsync(DbContext platform, DbContext sessions, CancellationToken token) =>
        [await ReadAsync(platform, PlatformQuery, false, token), await ReadAsync(sessions, SessionsQuery, false, token)];

    /// <summary>Retains every physical history field, including the old ProductVersion, independently of EF's ID-only history API.</summary>
    internal static async Task<string[][]> CaptureHistoryAsync(DbContext platform, DbContext sessions, CancellationToken token) =>
        [await ReadAsync(platform, PlatformHistory, false, token), await ReadAsync(sessions, SessionsHistory, false, token)];

    /// <summary>Requires every full old history row plus exactly one independently named native migration/version after each upgrade.</summary>
    internal static async Task RequireHistoryAsync(DbContext platform, DbContext sessions, string[][] before, string[] additions, CancellationToken token)
    {
        var after = await CaptureHistoryAsync(platform, sessions, token);
        for (var index = 0; index < before.Length; index++)
        {
            var expected = before[index].Append(JsonSerializer.Serialize(new[] { additions[index], ProductVersion })).Order(StringComparer.Ordinal);
            Assert.Equal(DatabaseSnapshot.Fingerprint(expected), DatabaseSnapshot.Fingerprint(after[index].Order(StringComparer.Ordinal)));
        }
    }

    /// <summary>Requires one appended null only, then compares the complete unchanged original rows and multiplicity.</summary>
    internal static async Task RequireAsync(DbContext platform, DbContext sessions, string[][] before, CancellationToken token)
    {
        var after = new[] { await ReadAsync(platform, PlatformQuery, true, token), await ReadAsync(sessions, SessionsQuery, true, token) };
        for (var index = 0; index < before.Length; index++)
        {
            Assert.Equal(RowCount, before[index].Length);
            Assert.Equal(DatabaseSnapshot.Fingerprint(before[index]), DatabaseSnapshot.Fingerprint(after[index]));
        }
    }

    /// <summary>Uses physical column order and typed values, refusing an absent, misplaced or populated new relation.</summary>
    private static async Task<string[]> ReadAsync(DbContext database, string query, bool upgraded, CancellationToken token)
    {
        var connection = database.Database.GetDbConnection();
        await connection.OpenAsync(token);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = CommandSeconds;
            await using var reader = await command.ExecuteReaderAsync(token);
            Assert.Equal(upgraded, reader.GetName(reader.FieldCount - 1) == Relation);
            var rows = new List<string>();
            while (await reader.ReadAsync(token))
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                if (upgraded)
                {
                    Assert.IsType<DBNull>(values[^1]);
                    values = values[..^1];
                }
                rows.Add(JsonSerializer.Serialize(values.Select(value => value is DBNull ? null : value)));
            }
            return rows.ToArray();
        }
        finally { await connection.CloseAsync(); }
    }
}
