using System.Globalization;
using System.Text.Json;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Independently specifies every new physical column's type, nullability and string width without consulting the candidate EF model.</summary>
internal static class ManagedSchemaColumns
{
    private const string Policy = "ApplicationPolicy";
    private const string Interaction = "ManagedLoginInteraction";
    private const string Schema = "platform";
    private const string Postgres = "PostgreSql";
    private const string Nullable = "YES";
    private const string Required = "NO";
    private const int NameIndex = 5;
    private const int NullableIndex = 8;
    private const int TypeIndex = 9;
    private const int LengthIndex = 10;
    private static readonly string[] PolicyNumbers = ["Revision", "CredentialRevision", "CredentialIssuedAt", "CredentialExpiresAt", "ObservedAt"];
    private static readonly string[] InteractionNumbers = ["ApplicationRevision", "Revision", "CreatedAt", "ExpiresAt", "ObservedAt", "RetainUntil"];
    private static readonly string[] OptionalNumbers = ["RequestExpiresAt", "NativeDependencyExpiresAt", "NativeDependencyCreatedAt", "AuthenticationGeneration", "FactorGeneration", "EnrollmentRevision", "PasswordVerifiedAt", "MfaVerifiedAt", "ConsentVerifiedAt"];
    private static readonly (string Name, int Length, bool Nullable)[] PolicyText = [("ApplicationId", 400, false), ("Profile", 14, false)];
    private static readonly (string Name, int Length, bool Nullable)[] InteractionText =
    [
        ("SelectorDigest", 64, false), ("BrowserDigest", 64, false), ("ApplicationId", 400, false), ("DemandDigest", 64, false),
        ("RequestId", 400, true), ("CeremonyBrowserDigest", 64, true), ("BrowserBudgetId", 64, true), ("UserId", 450, true),
        ("SecurityStamp", 256, true), ("AuthorizationId", 400, true), ("CodeId", 400, true)
    ];

    /// <summary>Requires exact independently enumerated column sets and provider-native facets, rejecting missing or unreviewed new fields.</summary>
    internal static void Require(string provider, string[] rows)
    {
        foreach (var table in new[] { Policy, Interaction })
        {
            var actual = rows.Select(row => JsonSerializer.Deserialize<string?[]>(row)!)
                .Where(values => values[0] == Schema && values[1] == table).ToDictionary(values => values[NameIndex]!, StringComparer.Ordinal);
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

    /// <summary>Uses independent physical expectations, retaining SQL Server's nullable GUID and complete native account identifier widths.</summary>
    private static Dictionary<string, (string Type, bool Nullable, int? Length)> Expected(string provider, string table)
    {
        const string Number = "bigint";
        const string PgText = "character varying";
        const string SqlText = "nvarchar";
        const string State = "State";
        const string Correlation = "CorrelationId";
        const string Ceremony = "CeremonyId";
        const string PgInteger = "integer";
        const string SqlInteger = "int";
        const string PgGuid = "uuid";
        const string SqlGuid = "uniqueidentifier";
        var expected = new Dictionary<string, (string, bool, int?)>(StringComparer.Ordinal);
        foreach (var field in table == Policy ? PolicyNumbers : InteractionNumbers)
        {
            expected.Add(field, (Number, false, null));
        }
        foreach (var field in table == Policy ? PolicyText : InteractionText)
        {
            expected.Add(field.Name, (provider == Postgres ? PgText : SqlText, field.Nullable, field.Length));
        }
        if (table == Interaction)
        {
            foreach (var field in OptionalNumbers)
            {
                expected.Add(field, (Number, true, null));
            }
            expected.Add(State, (provider == Postgres ? PgInteger : SqlInteger, false, null));
            expected.Add(Correlation, (provider == Postgres ? PgGuid : SqlGuid, false, null));
            expected.Add(Ceremony, (provider == Postgres ? PgGuid : SqlGuid, true, null));
        }
        return expected;
    }
}
