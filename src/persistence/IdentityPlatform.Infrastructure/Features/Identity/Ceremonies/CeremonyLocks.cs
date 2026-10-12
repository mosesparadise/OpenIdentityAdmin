using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Acquires the common portable UPDATE-lock order before any authoritative decision.</summary>
internal sealed class CeremonyLocks(PlatformDbContext database)
{
    /// <summary>Serializes optional global boundaries, sorted browsers, then sorted accounts; missing required rows deny.</summary>
    internal async Task AcquireAsync(CeremonyLockPlan plan, CancellationToken token)
    {
        if (database.Database.IsNpgsql())
        {
            const string LockTimeout = "SET LOCAL lock_timeout = '5s'";
            await database.Database.ExecuteSqlRawAsync(LockTimeout, token);
        }
        if (plan.Governance)
        {
            Require(await database.Set<AdministratorGovernance>().Where(row => row.Id == 1)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, row => row.Revision + 1), token));
        }
        if (plan.Admission)
        {
            Require(await database.Set<AdministratorAdmission>().Where(row => row.Id == 1)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, row => row.Revision + 1), token));
        }
        foreach (var id in plan.Browsers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            Require(await database.Set<AdministratorBrowserBudget>().Where(row => row.Id == id)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, row => row.Revision + 1), token));
        }
        foreach (var id in plan.Users.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            Require(await database.Set<AdministratorAuthentication>().Where(row => row.UserId == id)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, row => row.Revision + 1), token));
        }
    }

    /// <summary>Requires exactly one authoritative row rather than silently recreating missing coordination state.</summary>
    private static void Require(int count)
    {
        if (count != 1)
        {
            throw new FactorUnavailableException();
        }
    }
}

/// <summary>Declares the complete lock set before acquisition; helpers never acquire an earlier boundary afterward.</summary>
internal sealed class CeremonyLockPlan
{
    internal bool Governance
    {
        get;
    }
    internal bool Admission
    {
        get;
    }
    internal IReadOnlyList<string> Browsers
    {
        get;
    }
    internal IReadOnlyList<string> Users
    {
        get;
    }

    /// <summary>Snapshots and sorts caller input so later mutation cannot authorize an unacquired lock.</summary>
    internal CeremonyLockPlan(bool governance, bool admission, string[] browsers, string[] users)
    {
        Governance = governance;
        Admission = admission;
        Browsers = Array.AsReadOnly(browsers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        Users = Array.AsReadOnly(users.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
}
