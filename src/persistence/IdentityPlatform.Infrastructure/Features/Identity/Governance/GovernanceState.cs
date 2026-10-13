using System.Collections.Frozen;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Core.Features.Identity.Permissions;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Owns the freshly loaded pre-command state and original expected versions within one locked transaction.</summary>
internal sealed class GovernanceState
{
    private readonly CeremonyTransaction owner;
    internal GovernanceRequest Request
    {
        get;
    }
    internal CeremonyAccount Actor
    {
        get;
    }
    internal FrozenDictionary<string, CeremonyAccount> Accounts
    {
        get;
    }
    internal GovernanceGrantView Grants
    {
        get;
    }
    internal AdministratorGovernance Authority
    {
        get;
    }
    internal CeremonyAccount? Target => Request.UserId is { } id ? Accounts[id] : null;

    /// <summary>Captures only server-loaded accounts and current grants after validating the caller's target versions.</summary>
    private GovernanceState(CeremonyTransaction owner, GovernanceRequest request, string actorId,
        Dictionary<string, CeremonyAccount> accounts, GovernanceGrantView grants, AdministratorGovernance authority)
    {
        this.owner = owner;
        Request = request;
        Accounts = accounts.ToFrozenDictionary(StringComparer.Ordinal);
        Actor = Accounts[actorId];
        Grants = grants;
        Authority = authority;
    }

    /// <summary>Reads all declared state, requires current actor permissions and rejects stale explicit targets before password verification.</summary>
    internal static async Task<GovernanceState> ReadAsync(PlatformDbContext database, GovernanceGrants grants,
        CeremonyTransaction transaction, string actorId, GovernanceRequest request)
    {
        transaction.RequireContext(database);
        transaction.RequireGovernance();
        var accounts = new Dictionary<string, CeremonyAccount>(StringComparer.Ordinal);
        foreach (var id in transaction.Plan.Users)
        {
            accounts.Add(id, await CeremonyAccount.LoadAsync(database, transaction, id));
        }
        var view = await grants.ReadAsync(transaction, request.RoleId);
        var authority = await database.Set<AdministratorGovernance>().SingleAsync(row => row.Id == GovernanceLimits.Singleton, transaction.Token);
        var state = new GovernanceState(transaction, request, actorId, accounts, view, authority);
        state.RequireVersions();
        state.RequireAuthorized(transaction);
        return state;
    }

    /// <summary>Requires the exact original state owner and current pre-mutation permissions.</summary>
    internal void RequireAuthorized(CeremonyTransaction transaction)
    {
        transaction.RequireGovernance();
        Actor.RequireCurrent(transaction);
        if (!ReferenceEquals(owner, transaction) || !GovernancePermissions.Required(Request.Action).IsSubsetOf(Grants.Permissions(Actor.User.Id)))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Checks original intent once before legitimate native verification can rehash and change concurrency stamps.</summary>
    private void RequireVersions()
    {
        var expected = Request.Versions;
        if (Authority.AuthorityRevision != expected.AuthorityRevision || Authority.AuthorityRevision == long.MaxValue ||
            Target is { } target && (target.User.ConcurrencyStamp != expected.UserStamp || target.Enrollment.Revision != expected.EnrollmentRevision))
        {
            throw new FactorUnavailableException();
        }
        if (Request.RoleId is { } roleId && (Grants.Stamps.GetValueOrDefault(roleId) != expected.RoleStamp ||
            Request.Permission is { } permission && !AdministratorPermissions.Bundles[Grants.ReservedName(roleId)].Contains(permission)))
        {
            throw new FactorUnavailableException();
        }
        if (Request.RoleId is { } reservedId)
        {
            _ = Grants.ReservedName(reservedId);
        }
    }

    /// <summary>Detects unchanged requested state before any password check, replay reservation or accepted audit.</summary>
    internal bool IsNoChange() => Request.Action switch
    {
        GovernanceAction.AssignBundle => Grants.HasMembership(Request.UserId!, Request.RoleId!),
        GovernanceAction.RemoveBundle => !Grants.HasMembership(Request.UserId!, Request.RoleId!),
        GovernanceAction.AddGrant => Grants.HasGrant(Request.RoleId!, Request.Permission!),
        GovernanceAction.RemoveGrant => !Grants.HasGrant(Request.RoleId!, Request.Permission!),
        GovernanceAction.EnableAccount => Target!.User.IsEnabled,
        GovernanceAction.DisableAccount => !Target!.User.IsEnabled,
        GovernanceAction.SuspendAccount => Target!.Enrollment.Suspended,
        GovernanceAction.ResumeAccount => !Target!.Enrollment.Suspended,
        _ => throw new FactorUnavailableException()
    };
}
