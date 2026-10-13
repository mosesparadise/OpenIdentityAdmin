using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Records only bounded coordination labels and synthetic account IDs for one owned native transaction scope.</summary>
internal sealed class GovernanceLockTrace : DbCommandInterceptor
{
    private const string Update = "UPDATE";
    private const string Governance = "AdministratorGovernance";
    private const string Authentication = "AdministratorAuthentication";
    private const string Admission = "AdministratorAdmission";
    private const string Browser = "AdministratorBrowserBudget";
    private readonly List<string> locks = [];
    private PlatformDbContext? owner;
    private HashSet<string> known = new(StringComparer.Ordinal);

    /// <summary>Starts one bounded trace; known IDs include the new candidate so an attempted late lock cannot be hidden.</summary>
    internal void Arm(PlatformDbContext database, params string[] accountIds)
    {
        owner = database;
        known = accountIds.ToHashSet(StringComparer.Ordinal);
        locks.Clear();
    }

    /// <summary>Checks exact lock attempts and their order, including no extra global class or later account acquisition.</summary>
    internal void Require(CeremonyLockPlan original)
    {
        Assert.True(original.Governance);
        Assert.False(original.Admission);
        Assert.Empty(original.Browsers);
        Assert.Equal(new[] { Governance }.Concat(original.Users.Order(StringComparer.Ordinal)), locks);
    }

    /// <summary>Observes attempted UPDATE acquisition before execution so even a failed late lock appears in evidence.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (owner is null || !ReferenceEquals(owner, eventData.Context) || !command.CommandText.Contains(Update, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(result);
        }
        Assert.NotNull(owner.Database.CurrentTransaction);
        foreach (var table in new[] { Governance, Admission, Browser })
        {
            if (command.CommandText.Contains(table, StringComparison.Ordinal))
            {
                locks.Add(table);
                return ValueTask.FromResult(result);
            }
        }
        if (command.CommandText.Contains(Authentication, StringComparison.Ordinal))
        {
            var identifiers = command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value).OfType<string>().ToArray();
            var identifier = Assert.Single(identifiers);
            Assert.Contains(identifier, known);
            locks.Add(identifier);
        }
        return ValueTask.FromResult(result);
    }
}
