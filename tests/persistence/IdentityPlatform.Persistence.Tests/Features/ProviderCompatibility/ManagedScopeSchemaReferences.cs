using System.Text.Json;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Independently requires exact parent targets, restrictive delete behavior and reference storage facets from real engine metadata.</summary>
internal static class ManagedScopeSchemaReferences
{
    private const string Reference = "ManagedScopeReference";
    private const string ReferencesQuery = "information_schema.referential_constraints";
    private const string Schema = "platform";
    private const string Postgres = "PostgreSql";
    private const string NoAction = "NO ACTION";
    private const string Restrict = "RESTRICT";
    private const string PgText = "character varying";
    private const string SqlText = "nvarchar";
    private const string PgNative = "text";
    private const string Yes = "YES";
    private const string No = "NO";
    private const string Scope = "ScopeId";
    private const string Audience = "AudienceId";
    private const string Selector = "SelectorDigest";
    private const string DigestWidth = "64";
    private const string AudienceWidth = "100";
    private const string NativeWidth = "450";
    private const int NameIndex = 4;
    private const int TargetSchemaIndex = 6;
    private const int TargetKeyIndex = 7;
    private const int UpdateIndex = 9;
    private const int DeleteIndex = 10;
    private const int ColumnName = 5;
    private const int Nullable = 8;
    private const int Type = 9;
    private const int Length = 10;
    private static readonly Dictionary<string, string> Parents = new(StringComparer.Ordinal)
    {
        ["FK_ManagedScopeReference_ApiAudience_AudienceId"] = "PK_ApiAudience",
        ["FK_ManagedScopeReference_ManagedLoginInteraction_SelectorDigest"] = "PK_ManagedLoginInteraction",
        ["FK_ManagedScopeReference_OpenIddictScopes_ScopeId"] = "PK_OpenIddictScopes"
    };

    /// <summary>Rejects missing/extra parents, cascade changes, wrong parent keys and widened or nullable required identity fields.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> snapshot)
    {
        var query = Assert.Single(snapshot.Keys, value => value.Contains(ReferencesQuery, StringComparison.Ordinal));
        var references = snapshot[query].Select(Parse).Where(row => row[0] == Schema && row[1] == Reference).ToArray();
        Assert.Equal(Parents.Keys.Order(StringComparer.Ordinal), references.Select(row => row[NameIndex]).Order(StringComparer.Ordinal));
        foreach (var row in references)
        {
            Assert.Equal(Schema, row[TargetSchemaIndex]);
            Assert.Equal(Parents[row[NameIndex]!], row[TargetKeyIndex]);
            Assert.Equal(NoAction, row[UpdateIndex]);
            Assert.Equal(provider == Postgres ? Restrict : NoAction, row[DeleteIndex]);
        }
        var columns = snapshot[DatabaseSnapshot.Columns].Select(Parse).Where(row => row[0] == Schema && row[1] == Reference).ToArray();
        Assert.Equal([Audience, Scope, Selector], columns.Select(row => row[ColumnName]).Order(StringComparer.Ordinal));
        foreach (var row in columns)
        {
            var native = row[ColumnName] == Scope;
            Assert.Equal(row[ColumnName] == Audience ? Yes : No, row[Nullable]);
            Assert.Equal(provider == Postgres ? native ? PgNative : PgText : SqlText, row[Type]);
            Assert.Equal(native ? provider == Postgres ? null : NativeWidth : row[ColumnName] == Audience ? AudienceWidth : DigestWidth, row[Length]);
        }
    }

    /// <summary>Decodes the existing physical evidence format without consulting candidate model configuration.</summary>
    private static string?[] Parse(string value) => JsonSerializer.Deserialize<string?[]>(value)!;
}
