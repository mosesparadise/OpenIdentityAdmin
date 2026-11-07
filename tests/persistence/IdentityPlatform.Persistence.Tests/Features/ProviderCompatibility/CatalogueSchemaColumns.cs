using System.Globalization;
using System.Text.Json;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Independently specifies every new column's actual database type, nullability and width without reading candidate EF mappings.</summary>
internal static class CatalogueSchemaColumns
{
    private const string Audience = "ApiAudience";
    private const string Policy = "ScopePolicy";
    private const string Job = "CatalogueDeletion";
    private const string Schema = "platform";
    private const string Postgres = "PostgreSql";
    private const string Nullable = "YES";
    private const string Required = "NO";
    private const string PgText = "character varying";
    private const string SqlText = "nvarchar";
    private const string PgBoolean = "boolean";
    private const string SqlBoolean = "bit";
    private const string PgInteger = "integer";
    private const string SqlInteger = "int";
    private const string PgGuid = "uuid";
    private const string SqlGuid = "uniqueidentifier";
    private const string Number = "bigint";
    private const string NativeId = "ScopeId";
    private const string PgNativeId = "text";
    private const int SqlNativeIdLength = 450;
    private const string Revision = "Revision";
    private const string Enabled = "Enabled";
    private const string Completion = "CompletionAuditId";
    private const int NameIndex = 5;
    private const int NullableIndex = 8;
    private const int TypeIndex = 9;
    private const int LengthIndex = 10;
    private static readonly (string Name, int Length)[] AudienceText = [("Id", 100), ("Resource", 200), ("DisplayName", 200), ("Owner", 200)];
    private static readonly (string Name, int Length)[] PolicyText = [("ScopeId", 100), ("Kind", 8), ("ConsentText", 500), ("Disclosure", 5)];
    private static readonly (string Name, int Length)[] JobText = [("Kind", 8), ("TargetId", 100), ("ActorId", 450), ("SecurityStamp", 256), ("ScopedKeyHash", 64), ("RequestHash", 64)];
    private static readonly string[] JobNumbers = ["TargetRevision", "AuthenticationGeneration", "FactorGeneration", "EnrollmentRevision", "FactorVerifiedAt", "ApprovalUntil", "CreatedAt", "ObservedAt"];
    private static readonly string[] JobIntegers = ["State", "Failure"];
    private static readonly string[] JobGuids = ["Id", "RequestKey", "AdmissionAuditId", Completion];

    /// <summary>Rejects omitted, unexpected, nullable, widened and narrowed fields while preserving native provider representations.</summary>
    internal static void Require(string provider, string[] rows)
    {
        foreach (var table in new[] { Audience, Policy, Job })
        {
            var actual = rows.Select(row => JsonSerializer.Deserialize<string?[]>(row)!)
                .Where(row => row[0] == Schema && row[1] == table).ToDictionary(row => row[NameIndex]!, StringComparer.Ordinal);
            var expected = Expected(provider, table);
            Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
            foreach (var (name, facets) in expected)
            {
                var value = actual[name];
                Assert.Equal(facets.Type, value[TypeIndex]);
                Assert.Equal(facets.Nullable ? Nullable : Required, value[NullableIndex]);
                Assert.Equal(facets.Length?.ToString(CultureInfo.InvariantCulture), value[LengthIndex]);
            }
        }
    }

    /// <summary>Builds independently named physical expectations; no model constants can mask a changed database bound.</summary>
    private static Dictionary<string, (string Type, bool Nullable, int? Length)> Expected(string provider, string table)
    {
        var expected = new Dictionary<string, (string, bool, int?)>(StringComparer.Ordinal) { [Revision] = (Number, false, null) };
        foreach (var field in table == Audience ? AudienceText : table == Policy ? PolicyText : JobText)
        {
            expected.Add(field.Name, (provider == Postgres ? PgText : SqlText, false, field.Length));
        }
        if (table == Policy)
        {
            expected[NativeId] = (provider == Postgres ? PgNativeId : SqlText, false, provider == Postgres ? null : SqlNativeIdLength);
        }
        if (table != Job)
        {
            expected.Add(Enabled, (provider == Postgres ? PgBoolean : SqlBoolean, false, null));
            return expected;
        }
        foreach (var field in JobNumbers)
        {
            expected.Add(field, (Number, false, null));
        }
        foreach (var field in JobIntegers)
        {
            expected.Add(field, (provider == Postgres ? PgInteger : SqlInteger, false, null));
        }
        foreach (var field in JobGuids)
        {
            expected.Add(field, (provider == Postgres ? PgGuid : SqlGuid, field == Completion, null));
        }
        return expected;
    }
}
