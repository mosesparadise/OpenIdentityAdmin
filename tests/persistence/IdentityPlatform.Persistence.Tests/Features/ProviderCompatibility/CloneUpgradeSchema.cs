using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Checks provenance column bounds, required values and the sole audit parent against independent engine metadata.</summary>
internal static class CloneUpgradeSchema
{
    private const string Schema = "platform";
    private const string Table = "ApplicationCloneAuditDetail";
    private const string Postgres = "PostgreSql";
    private const string Constraints = "information_schema.table_constraints";
    private const string Keys = "information_schema.key_column_usage";
    private const string PrimaryKey = "PK_ApplicationCloneAuditDetail";
    private const string PrimaryType = "PRIMARY KEY";
    private const string FirstOrdinal = "1";
    private const int ConstraintType = 8;
    private const int KeyColumn = 8;
    private const int KeyOrdinal = 9;
    private const string References = "information_schema.referential_constraints";
    private const string Audit = "AuditId";
    private const string Source = "SourceApplicationId";
    private const string Version = "SourceVersion";
    private const string Required = "NO";
    private const string PgGuid = "uuid";
    private const string SqlGuid = "uniqueidentifier";
    private const string PgText = "character varying";
    private const string SqlText = "nvarchar";
    private const string SourceWidth = "400";
    private const string VersionWidth = "86";
    private const string ForeignKey = "FK_ApplicationCloneAuditDetail_SecurityAuditIntent_AuditId";
    private const string ParentKey = "PK_SecurityAuditIntent";
    private const string NoAction = "NO ACTION";
    private const string Cascade = "CASCADE";
    private const int Name = 5;
    private const int Nullable = 8;
    private const int Type = 9;
    private const int Length = 10;
    private const int ConstraintName = 4;
    private const int TargetSchema = 6;
    private const int TargetKey = 7;
    private const int Update = 9;
    private const int Delete = 10;

    /// <summary>Rejects nullable, unbounded, incorrectly typed fields or changed audit ownership/delete behavior.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> snapshot)
    {
        RequireKeys(snapshot);
        var columns = snapshot[DatabaseSnapshot.Columns].Select(Parse).Where(Owns).ToArray();
        Assert.Equal([Audit, Source, Version], columns.Select(row => row[Name]).Order(StringComparer.Ordinal));
        foreach (var column in columns)
        {
            Assert.Equal(Required, column[Nullable]);
            var guid = column[Name] == Audit;
            Assert.Equal(guid ? provider == Postgres ? PgGuid : SqlGuid : provider == Postgres ? PgText : SqlText, column[Type]);
            Assert.Equal(guid ? null : column[Name] == Source ? SourceWidth : VersionWidth, column[Length]);
        }
        var query = Assert.Single(snapshot.Keys, value => value.Contains(References, StringComparison.Ordinal));
        var reference = Assert.Single(snapshot[query].Select(Parse), Owns);
        Assert.Equal(ForeignKey, reference[ConstraintName]);
        Assert.Equal(Schema, reference[TargetSchema]);
        Assert.Equal(ParentKey, reference[TargetKey]);
        Assert.Equal(NoAction, reference[Update]);
        Assert.Equal(Cascade, reference[Delete]);
    }

    /// <summary>Requires a real primary key on the audit identity and exactly the same child column for its audit foreign key.</summary>
    private static void RequireKeys(Dictionary<string, string[]> snapshot)
    {
        var query = Assert.Single(snapshot.Keys, value => value.Contains(Keys, StringComparison.Ordinal));
        var keys = snapshot[query].Select(Parse).Where(Owns).ToArray();
        Assert.Equal(new[] { ForeignKey, PrimaryKey }.Order(StringComparer.Ordinal), keys.Select(row => row[ConstraintName]).Order(StringComparer.Ordinal));
        Assert.All(keys, row => { Assert.Equal(Audit, row[KeyColumn]); Assert.Equal(FirstOrdinal, row[KeyOrdinal]); });
        var constraints = Assert.Single(snapshot.Keys, value => value.Contains(Constraints, StringComparison.Ordinal) && !value.Contains(References, StringComparison.Ordinal));
        var primary = Assert.Single(snapshot[constraints].Select(Parse), row => Owns(row) && row[ConstraintType] == PrimaryType);
        Assert.Equal(PrimaryKey, primary[ConstraintName]);
    }

    /// <summary>Demonstrates that missing required column metadata and missing audit ownership cannot satisfy the witness.</summary>
    internal static void RejectDefects(string provider, Dictionary<string, string[]> snapshot)
    {
        var keys = Assert.Single(snapshot.Keys, value => value.Contains(Keys, StringComparison.Ordinal));
        var missingKey = new Dictionary<string, string[]>(snapshot) { [keys] = snapshot[keys].Where(row => !Owns(Parse(row)) || Parse(row)[ConstraintName] != PrimaryKey).ToArray() };
        Assert.ThrowsAny<XunitException>(() => Require(provider, missingKey));
        var changedKey = new Dictionary<string, string[]>(snapshot)
        {
            [keys] = snapshot[keys].Select(row =>
            {
                var values = Parse(row);
                if (Owns(values))
                {
                    values[KeyColumn] = Source;
                }
                return JsonSerializer.Serialize(values);
            }).ToArray()
        };
        Assert.ThrowsAny<XunitException>(() => Require(provider, changedKey));
        var columns = new Dictionary<string, string[]>(snapshot)
        {
            [DatabaseSnapshot.Columns] = snapshot[DatabaseSnapshot.Columns].Where(row => !Owns(Parse(row)) || Parse(row)[Name] != Source).ToArray()
        };
        Assert.ThrowsAny<XunitException>(() => Require(provider, columns));
        var query = Assert.Single(snapshot.Keys, value => value.Contains(References, StringComparison.Ordinal));
        var parent = new Dictionary<string, string[]>(snapshot) { [query] = snapshot[query].Where(row => !Owns(Parse(row))).ToArray() };
        Assert.ThrowsAny<XunitException>(() => Require(provider, parent));
    }

    /// <summary>Requires exact metadata owner fields.</summary>
    private static bool Owns(string?[] values) => values[0] == Schema && values[1] == Table;

    /// <summary>Reads the immutable snapshot encoding without candidate EF metadata.</summary>
    private static string?[] Parse(string row) => JsonSerializer.Deserialize<string?[]>(row)!;
}
