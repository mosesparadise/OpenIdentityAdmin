using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Policy;

/// <summary>Observes only synthetic collection-query shape, bound values and account-lock IDs without retaining SQL or credential parameters.</summary>
internal sealed class CapacitySqlEvidence : DbCommandInterceptor
{
    private const string RolesTable = "AspNetRoles";
    private const string MembershipTable = "AspNetUserRoles";
    private const string ClaimsTable = "AspNetRoleClaims";
    private const string AccountTable = "AdministratorAuthentication";
    private const string UserColumn = "UserId";
    private const string RoleColumn = "RoleId";
    private const string Order = "ORDER BY";
    private const string Where = "WHERE";
    private const string Update = "UPDATE";
    private const string BoundedMessage = "The actual authority collection read lacked its independently expected provider-side limit-plus-one.";
    private const string FilterMessage = "The bounded authority read filtered more than the exact owner before ordinal validation.";
    private const string LimitPattern = @"\bLIMIT\s+(?<parameter>@\w+)|\bTOP\s*\(\s*(?<parameter>@\w+)\s*\)";
    private const string ParameterGroup = "parameter";
    private const string AliasPattern = @"(?:\w+|\[\w+\]|""\w+"")\.";
    private readonly HashSet<CapacityKind> observed = [];
    private readonly HashSet<string> memberRoles = new(StringComparer.Ordinal);
    private readonly HashSet<string> accountLocks = new(StringComparer.Ordinal);
    private CapacityCase? test;
    private string[] expectedRoles = [];
    private bool active;
    internal IReadOnlySet<string> AccountLocks => accountLocks;

    /// <summary>Arms only the actual decision after seeding, expected-version reads and before-state snapshots have completed.</summary>
    internal void Begin(CapacityCase current, string[] accountRoles)
    {
        test = current;
        expectedRoles = [.. accountRoles];
        observed.Clear();
        memberRoles.Clear();
        accountLocks.Clear();
        active = true;
    }

    /// <summary>Excludes independent after-state assertions and their intentionally unbounded test-only fixture reads.</summary>
    internal void Stop() => active = false;

    /// <summary>Requires the selected collection's evidence and both account-member sources for the union boundary.</summary>
    internal void RequireBoundedRetrieval()
    {
        Assert.NotNull(test);
        Assert.Contains(test.Kind, observed);
        if (test.Kind == CapacityKind.Accounts)
        {
            Assert.Equal(expectedRoles.Order(StringComparer.Ordinal), memberRoles.Order(StringComparer.Ordinal));
        }
    }

    /// <summary>Inspects successfully executed provider reads; no replacement result or policy behavior is supplied.</summary>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (active && test is not null && Classify(command.CommandText) == test.Kind)
        {
            RequireLimit(command, test.Limit + 1);
            RequireFilter(command, test.Kind);
            observed.Add(test.Kind);
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Collects the real account-lock identities so a successful command must lock the entire independently constructed union.</summary>
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (active && ContainsTable(command.CommandText, AccountTable) && command.CommandText.Contains(Update, StringComparison.Ordinal))
        {
            Assert.Equal(1, result);
            accountLocks.Add(Assert.Single(command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value).OfType<string>()));
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Distinguishes full ordered collection reads from unrelated native single-key lookups.</summary>
    private static CapacityKind? Classify(string sql)
    {
        if (!sql.Contains(Order, StringComparison.Ordinal))
        {
            return null;
        }
        if (ContainsTable(sql, RolesTable))
        {
            return CapacityKind.Roles;
        }
        if (ContainsTable(sql, ClaimsTable))
        {
            return CapacityKind.Claims;
        }
        if (!ContainsTable(sql, MembershipTable))
        {
            return null;
        }
        var filter = Predicate(sql);
        return filter.Contains(UserColumn, StringComparison.Ordinal) ? CapacityKind.Memberships : CapacityKind.Accounts;
    }

    /// <summary>Requires an actual LIMIT/TOP parameter with an independently specified value, never merely a correctly sized result.</summary>
    private static void RequireLimit(DbCommand command, int expected)
    {
        var match = Regex.Match(command.CommandText, LimitPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, BoundedMessage);
        var name = match.Groups[ParameterGroup].Value;
        var parameter = Assert.Single(command.Parameters.Cast<DbParameter>(), value => value.ParameterName.TrimStart('@') == name.TrimStart('@'));
        Assert.Equal(expected, Convert.ToInt32(parameter.Value, CultureInfo.InvariantCulture));
    }

    /// <summary>Rejects hidden permission/name filtering; account-member evidence additionally records both affected and platform role IDs.</summary>
    private void RequireFilter(DbCommand command, CapacityKind kind)
    {
        var predicate = Predicate(command.CommandText);
        if (kind == CapacityKind.Roles)
        {
            Assert.True(predicate.Length == 0, FilterMessage);
            return;
        }
        var column = kind == CapacityKind.Memberships ? UserColumn : RoleColumn;
        const string OwnerPrefix = @"^\s*";
        const string QuotedPrefix = "(?:\"";
        const string AlternateQuote = "\"|\\[";
        const string OwnerSuffix = @"\])\s*=\s*@\w+\s*$";
        var pattern = OwnerPrefix + AliasPattern + QuotedPrefix + column + AlternateQuote + column + OwnerSuffix;
        Assert.True(Regex.IsMatch(predicate, pattern, RegexOptions.CultureInvariant), FilterMessage);
        if (kind == CapacityKind.Accounts)
        {
            memberRoles.Add(Assert.Single(command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value).OfType<string>()));
        }
    }

    /// <summary>Extracts only the owner predicate of the pinned EF collection query without retaining the command.</summary>
    private static string Predicate(string sql)
    {
        var where = sql.IndexOf(Where, StringComparison.Ordinal);
        return where < 0 ? string.Empty : sql[(where + Where.Length)..sql.IndexOf(Order, where, StringComparison.Ordinal)].Trim();
    }

    /// <summary>Recognizes exact provider-quoted table tokens without conflating native role and role-claim tables.</summary>
    private static bool ContainsTable(string sql, string table) =>
        sql.Contains('"' + table + '"', StringComparison.Ordinal) || sql.Contains('[' + table + ']', StringComparison.Ordinal);
}
