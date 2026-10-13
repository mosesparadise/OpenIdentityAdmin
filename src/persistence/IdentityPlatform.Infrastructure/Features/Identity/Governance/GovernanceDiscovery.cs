using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Core.Features.Identity.Permissions;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Discovers the complete bounded union before locks and rejects any required expansion after serialization.</summary>
internal sealed class GovernanceDiscovery(PlatformDbContext database, GovernanceGrants grants)
{
    /// <summary>Snapshots actor, explicit target, affected role members and potential platform survivors in native lock order.</summary>
    internal async Task<CeremonyLockPlan> DiscoverAsync(string actorId, GovernanceRequest? request, CancellationToken token)
    {
        if (!GovernanceRequest.IsIdentifier(actorId))
        {
            throw new FactorUnavailableException();
        }
        var roles = await grants.ReadRolesAsync(token);
        var accounts = new HashSet<string>(StringComparer.Ordinal) { actorId };
        if (request?.UserId is { } userId)
        {
            accounts.Add(userId);
        }
        foreach (var role in roles.Where(role => role.Name == AdministratorPermissions.PlatformRole || role.Id == request?.RoleId))
        {
            var members = await database.UserRoles.AsNoTracking().Where(row => row.RoleId == role.Id)
                .OrderBy(row => row.UserId).Take(GovernanceLimits.Accounts + 1).ToListAsync(token);
            if (members.Any(row => row.RoleId != role.Id || !GovernanceRequest.IsIdentifier(row.UserId)))
            {
                throw new FactorUnavailableException();
            }
            accounts.UnionWith(members.Select(row => row.UserId));
            if (members.Count > GovernanceLimits.Accounts || accounts.Count > GovernanceLimits.Accounts)
            {
                throw new FactorUnavailableException();
            }
        }
        return new CeremonyLockPlan(true, false, [], accounts.ToArray());
    }

    /// <summary>Rejects changed relationships that would require late lock acquisition or an inconsistent authority view.</summary>
    internal async Task RequireUnchangedAsync(CeremonyTransaction transaction, string actorId, GovernanceRequest? request)
    {
        transaction.RequireContext(database);
        transaction.RequireGovernance();
        var current = await DiscoverAsync(actorId, request, transaction.Token);
        if (!current.Users.SequenceEqual(transaction.Plan.Users, StringComparer.Ordinal))
        {
            throw new FactorUnavailableException();
        }
    }
}
