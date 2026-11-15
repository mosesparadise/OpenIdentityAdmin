using System.Text.Json;
using Xunit.Sdk;
using static IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility.RetirementUpgradeWitness;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Requires exact independent retirement constraint expressions, normalizing only engine formatting and equivalent boolean-to-integer casts.</summary>
internal static class RetirementUpgradeChecks
{
    private const string TerminalExpression = "retired+enabled<=1";
    private const string RevisionExpression = "originalrevision>0andretiredrevision-originalrevision=1";
    private const string TimeExpression = "retiredat>=0andreplayuntil>retiredatandreplayuntil-retiredat=86400";
    private const string PostgresReasonExpression = "reason=anyarray1,2,3";
    private const string SqlReasonExpression = "reason=3orreason=2orreason=1";
    private const string RetiredField = "retired";
    private const string EnabledField = "enabled";
    private const string PostgresIntegerCast = "::integer";
    private const string Cast = "cast";
    private const string AsInteger = "asinteger";
    private const string AsInt = "asint";
    private const string ConvertInt = "convertint,";

    private const string Postgres = "PostgreSql";
    private const int Name = 4;
    private const int Definition = 5;
    private const string Reason = "CK_ApplicationRetirement_Reason";
    private const string Revision = "CK_ApplicationRetirement_Revision";
    private const string Time = "CK_ApplicationRetirement_Time";
    private const string TrueExpression = "1=1";
    private const string UnexpectedCheck = "CK_ApplicationRetirement_Unexpected";
    private const string RequiredSuffix = "_not_null";
    private const string Separator = "_";
    private static readonly string[] RequiredFields = ["ApplicationId", "ActorId", "OriginalRevision", "RetiredRevision", "Reason", "RetiredAt", "RequestKey", "ScopedKeyHash", "RequestHash", "ReplayUntil", "AuditId", "GrantJobId", "GrantApprovalId"];
    private static readonly string[] Names = [TerminalCheck, Reason, Revision, Time];
    private static readonly char[] Formatting = [' ', '\r', '\n', '\t', '(', ')', '[', ']', '"'];

    /// <summary>Rejects absent or permissive terminal/revision/replay/reason checks using physical database definitions, never model constants.</summary>
    internal static void Require(string provider, Dictionary<string, string[]> snapshot)
    {
        var query = provider == Postgres ? MigrationCompatibilityTests.PostgresChecks : MigrationCompatibilityTests.SqlChecks;
        var rows = snapshot[query].Select(Parse).Where(row => row[0] == Schema).ToArray();
        var expectedNames = new[] { Reason, Revision, Time }.Concat(provider == Postgres ? RequiredFields.Select(field => Table + Separator + field + RequiredSuffix) : []);
        Assert.Equal(expectedNames.Order(StringComparer.Ordinal), rows.Where(row => row[1] == Table).Select(row => row[Name]).Order(StringComparer.Ordinal));
        foreach (var name in Names)
        {
            var row = Assert.Single(rows, row => row[Name] == name);
            Assert.Equal(name == TerminalCheck ? Policy : Table, row[1]);
            var expected = name switch
            {
                TerminalCheck => TerminalExpression,
                Revision => RevisionExpression,
                Time => TimeExpression,
                _ => provider == Postgres ? PostgresReasonExpression : SqlReasonExpression
            };
            Assert.Equal(expected, Normalize(row[Definition]!));
        }
    }

    /// <summary>Calibrates each check independently against both removal and a valid but nonrestrictive replacement expression.</summary>
    internal static void RejectDefects(string provider, Dictionary<string, string[]> snapshot)
    {
        var query = provider == Postgres ? MigrationCompatibilityTests.PostgresChecks : MigrationCompatibilityTests.SqlChecks;
        foreach (var name in Names)
        {
            var missing = snapshot[query].Where(row => Parse(row)[Name] != name).ToArray();
            Assert.ThrowsAny<XunitException>(() => Require(provider, new(snapshot) { [query] = missing }));
            var weakened = snapshot[query].Select(row =>
            {
                var value = Parse(row);
                if (value[Name] == name)
                {
                    value[Definition] = TrueExpression;
                }
                return JsonSerializer.Serialize(value);
            }).ToArray();
            Assert.ThrowsAny<XunitException>(() => Require(provider, new(snapshot) { [query] = weakened }));
        }
        var extra = Parse(Assert.Single(snapshot[query], row => Parse(row)[Name] == Reason));
        extra[Name] = UnexpectedCheck;
        var additional = snapshot[query].Append(JsonSerializer.Serialize(extra)).ToArray();
        Assert.ThrowsAny<XunitException>(() => Require(provider, new(snapshot) { [query] = additional }));
    }

    /// <summary>Ignores cosmetic provider syntax while preserving all operators, identifiers and numeric bounds.</summary>
    private static string Normalize(string value)
    {
        var text = new string(value.Where(character => !Formatting.Contains(character)).ToArray()).ToLowerInvariant();
        foreach (var field in new[] { RetiredField, EnabledField })
        {
            text = text.Replace(field + PostgresIntegerCast, field, StringComparison.Ordinal)
                .Replace(Cast + field + AsInteger, field, StringComparison.Ordinal)
                .Replace(Cast + field + AsInt, field, StringComparison.Ordinal)
                .Replace(ConvertInt + field, field, StringComparison.Ordinal);
        }
        return text;
    }
}
