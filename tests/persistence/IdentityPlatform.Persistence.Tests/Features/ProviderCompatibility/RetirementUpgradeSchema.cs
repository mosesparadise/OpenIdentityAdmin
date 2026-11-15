using System.Text.Json;
using Xunit.Sdk;
using static IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility.RetirementUpgradeWitness;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Requires independently specified physical column bounds, default, restrictive parents and terminal checks on each engine.</summary>
internal static class RetirementUpgradeSchema
{
    private const string PostgresBoolean = "boolean";
    private const string SqlBoolean = "bit";

    private const string Postgres = "PostgreSql";
    private const string Required = "NO";
    private const string Nullable = "YES";
    private const string PgFalse = "false";
    private const string SqlFalse = "(CONVERT([bit],(0)))";
    private const string References = "information_schema.referential_constraints";
    private const string PrimaryKeys = "information_schema.key_column_usage";
    private const string Primary = "PK_ApplicationRetirement";
    private const string ApplicationId = "ApplicationId";
    private const string Restrict = "RESTRICT";
    private const string NoAction = "NO ACTION";
    private const string Cascade = "CASCADE";
    private const string First = "1";
    private const int Name = 5;
    private const int Default = 7;
    private const int Nullability = 8;
    private const int Type = 9;
    private const int Width = 10;
    private const int SqlCollation = 21;
    private const string OrdinalCollation = "Latin1_General_100_BIN2";
    private const string WrongCollation = "Latin1_General_CI_AS";
    private const int Constraint = 4;
    private const int ParentSchema = 6;
    private const int ParentKey = 7;
    private const int Update = 9;
    private const int Delete = 10;
    private const int KeyColumn = 8;
    private const int KeyOrdinal = 9;
    private static readonly (string Name, string Pg, string Sql, string? Width)[] Columns =
    [
        ("ApplicationId", "character varying", "nvarchar", "400"), ("ActorId", "character varying", "nvarchar", "450"),
        ("ScopedKeyHash", "character varying", "nvarchar", "64"), ("RequestHash", "character varying", "nvarchar", "64"),
        ("OriginalRevision", "bigint", "bigint", null), ("RetiredRevision", "bigint", "bigint", null),
        ("RetiredAt", "bigint", "bigint", null), ("ReplayUntil", "bigint", "bigint", null), ("Reason", "integer", "int", null),
        ("RequestKey", "uuid", "uniqueidentifier", null), ("AuditId", "uuid", "uniqueidentifier", null),
        ("GrantJobId", "uuid", "uniqueidentifier", null), ("GrantApprovalId", "uuid", "uniqueidentifier", null)
    ];
    private static readonly (string Pg, string Sql, string Column, string Parent)[] Parents =
    [
        ("FK_ApplicationRetirement_ApplicationPolicy_ApplicationId", "FK_ApplicationRetirement_ApplicationPolicy_ApplicationId", "ApplicationId", "PK_ApplicationPolicy"),
        ("FK_ApplicationRetirement_SecurityAuditIntent_AuditId", "FK_ApplicationRetirement_SecurityAuditIntent_AuditId", "AuditId", "PK_SecurityAuditIntent"),
        ("FK_ApplicationRetirement_ApplicationGrantJob_GrantJobId", "FK_ApplicationRetirement_ApplicationGrantJob_GrantJobId", "GrantJobId", "PK_ApplicationGrantJob"),
        ("FK_ApplicationRetirement_ApplicationGrantApproval_GrantApprova~", "FK_ApplicationRetirement_ApplicationGrantApproval_GrantApprovalId", "GrantApprovalId", "PK_ApplicationGrantApproval")
    ];

    /// <summary>Checks every required field and false terminal default before validating independent parent and check metadata.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> snapshot)
    {
        var columns = snapshot[DatabaseSnapshot.Columns].Select(Parse).ToArray();
        var retired = Assert.Single(columns, row => row[0] == Schema && row[1] == Policy && row[Name] == Retired);
        Assert.Equal(Required, retired[Nullability]);
        Assert.Equal(provider == Postgres ? PostgresBoolean : SqlBoolean, retired[Type]);
        Assert.Equal(provider == Postgres ? PgFalse : SqlFalse, retired[Default]);
        var receipt = columns.Where(Owns).ToArray();
        Assert.Equal(Columns.Select(row => row.Name).Order(StringComparer.Ordinal), receipt.Select(row => row[Name]).Order(StringComparer.Ordinal));
        foreach (var expected in Columns)
        {
            var column = Assert.Single(receipt, row => row[Name] == expected.Name);
            Assert.Equal(Required, column[Nullability]);
            Assert.Equal(provider == Postgres ? expected.Pg : expected.Sql, column[Type]);
            Assert.Equal(expected.Width, column[Width]);
            if (provider != Postgres && expected.Width is not null)
            {
                Assert.Equal(OrdinalCollation, column[SqlCollation]);
            }
        }
        RequireParents(provider, snapshot);
        RetirementUpgradeIndexes.Require(provider, snapshot);
        RetirementUpgradeChecks.Require(provider, snapshot);
    }

    /// <summary>Requires four restrictive exact parent bindings plus the sole primary application identity.</summary>
    private static void RequireParents(string provider, Dictionary<string, string[]> snapshot)
    {
        var query = Assert.Single(snapshot.Keys, value => value.Contains(References, StringComparison.Ordinal));
        var parents = snapshot[query].Select(Parse).Where(Owns).ToArray();
        Assert.Equal(Parents.Length, parents.Length);
        var keys = snapshot[Assert.Single(snapshot.Keys, value => value.Contains(PrimaryKeys, StringComparison.Ordinal))].Select(Parse).Where(Owns).ToArray();
        Assert.Equal(Parents.Length + 1, keys.Length);
        foreach (var expected in Parents)
        {
            var name = provider == Postgres ? expected.Pg : expected.Sql;
            var parent = Assert.Single(parents, row => row[Constraint] == name);
            Assert.Equal(Schema, parent[ParentSchema]);
            Assert.Equal(expected.Parent, parent[ParentKey]);
            Assert.Equal(NoAction, parent[Update]);
            Assert.Equal(provider == Postgres ? Restrict : NoAction, parent[Delete]);
            var key = Assert.Single(keys, row => row[Constraint] == name);
            Assert.Equal(expected.Column, key[KeyColumn]);
            Assert.Equal(First, key[KeyOrdinal]);
        }
        var primary = Assert.Single(keys, row => row[Constraint] == Primary);
        Assert.Equal(ApplicationId, primary[KeyColumn]);
        Assert.Equal(First, primary[KeyOrdinal]);
    }

    /// <summary>Shows that nullable retirement metadata, missing ownership and cascading deletion cannot satisfy the independent schema witness.</summary>
    internal static void RejectDefects(string provider, Dictionary<string, string[]> snapshot)
    {
        var columns = snapshot[DatabaseSnapshot.Columns].Select(row =>
        {
            var values = Parse(row);
            if (values[1] == Policy && values[Name] == Retired)
            {
                values[Nullability] = Nullable;
            }
            return JsonSerializer.Serialize(values);
        }).ToArray();
        Assert.ThrowsAny<XunitException>(() => Require(provider, new(snapshot) { [DatabaseSnapshot.Columns] = columns }));
        var query = Assert.Single(snapshot.Keys, value => value.Contains(References, StringComparison.Ordinal));
        var missing = snapshot[query].Where(row => !Owns(Parse(row))).ToArray();
        Assert.ThrowsAny<XunitException>(() => Require(provider, new(snapshot) { [query] = missing }));
        var cascade = snapshot[query].Select(row =>
        {
            var values = Parse(row);
            if (Owns(values))
            {
                values[Delete] = Cascade;
            }
            return JsonSerializer.Serialize(values);
        }).ToArray();
        Assert.ThrowsAny<XunitException>(() => Require(provider, new(snapshot) { [query] = cascade }));
        RetirementUpgradeIndexes.RejectDefects(provider, snapshot);
        RetirementUpgradeChecks.RejectDefects(provider, snapshot);
        RejectCollation(provider, snapshot);
    }

    /// <summary>Rejects one physically case-insensitive identifier column even when its type, width and all other metadata remain unchanged.</summary>
    private static void RejectCollation(string provider, Dictionary<string, string[]> snapshot)
    {
        if (provider == Postgres)
        {
            return;
        }
        var changed = snapshot[DatabaseSnapshot.Columns].Select(row =>
        {
            var values = Parse(row);
            if (Owns(values) && values[Name] == ApplicationId)
            {
                values[SqlCollation] = WrongCollation;
            }
            return JsonSerializer.Serialize(values);
        }).ToArray();
        Assert.ThrowsAny<XunitException>(() => Require(provider, new(snapshot) { [DatabaseSnapshot.Columns] = changed }));
    }

    /// <summary>Matches exact physical schema/table owners rather than resemblance in free metadata text.</summary>
    private static bool Owns(string?[] row) => row[0] == Schema && row[1] == Table;
}
