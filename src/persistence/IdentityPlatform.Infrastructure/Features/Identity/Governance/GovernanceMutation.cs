using System.Security.Claims;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Core.Features.Identity.Permissions;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Applies the fixed command catalogue through native managers and checked product transitions in the caller's transaction.</summary>
internal sealed class GovernanceMutation(PlatformDbContext database, UserManager<PlatformUser> users,
    RoleManager<IdentityRole> roles, JourneyRevocation revocation)
{
    /// <summary>Dispatches only after proof consumption; manager failures abort all writes instead of producing partial acceptance.</summary>
    internal async Task ApplyAsync(CeremonyTransaction transaction, GovernanceState state, GovernanceProof proof, long now)
    {
        transaction.RequireContext(database);
        proof.RequireConsumed(transaction, state.Request, now);
        ((AdministratorUserManager)users).RequireOperation(database, transaction.Token);
        ((GovernanceRoleManager)roles).RequireOperation(database, transaction.Token);
        var request = state.Request;
        switch (request.Action)
        {
            case GovernanceAction.AssignBundle:
                Require(await users.AddToRoleAsync(state.Target!.User, state.Grants.ReservedName(request.RoleId!)));
                break;
            case GovernanceAction.RemoveBundle:
                Require(await users.RemoveFromRoleAsync(state.Target!.User, state.Grants.ReservedName(request.RoleId!)));
                break;
            case GovernanceAction.AddGrant:
            case GovernanceAction.RemoveGrant:
                await ChangeGrantAsync(transaction, request);
                break;
            default:
                await ChangeAccountAsync(transaction, state);
                break;
        }
        state.Authority.AuthorityRevision = checked(state.Authority.AuthorityRevision + 1);
        await database.SaveChangesAsync(transaction.Token);
    }

    /// <summary>Mutates exactly one validated permission on its reserved native role.</summary>
    private async Task ChangeGrantAsync(CeremonyTransaction transaction, GovernanceRequest request)
    {
        var role = await roles.FindByIdAsync(request.RoleId!) ?? throw new FactorUnavailableException();
        if (role.Id != request.RoleId || role.ConcurrencyStamp != request.Versions.RoleStamp)
        {
            throw new FactorUnavailableException();
        }
        var claim = new Claim(AdministratorPermissions.ClaimType, request.Permission!);
        Require(request.Action == GovernanceAction.AddGrant ? await roles.AddClaimAsync(role, claim) : await roles.RemoveClaimAsync(role, claim));
        transaction.RequireCurrent();
    }

    /// <summary>Advances generations and revokes prior authority for disable/suspend; reactivation never resurrects old sessions.</summary>
    private async Task ChangeAccountAsync(CeremonyTransaction transaction, GovernanceState state)
    {
        var account = state.Target ?? throw new FactorUnavailableException();
        switch (state.Request.Action)
        {
            case GovernanceAction.EnableAccount:
            case GovernanceAction.DisableAccount:
                account.User.IsEnabled = state.Request.Action == GovernanceAction.EnableAccount;
                Require(await users.UpdateAsync(account.User));
                break;
            case GovernanceAction.SuspendAccount:
            case GovernanceAction.ResumeAccount:
                account.Enrollment.Suspended = state.Request.Action == GovernanceAction.SuspendAccount;
                break;
            default:
                throw new FactorUnavailableException();
        }
        account.Enrollment.Revision = checked(account.Enrollment.Revision + 1);
        if (state.Request.Action is GovernanceAction.DisableAccount or GovernanceAction.SuspendAccount)
        {
            account.Authentication.Generation = checked(account.Authentication.Generation + 1);
            await revocation.RevokeAsync(transaction, account);
        }
    }

    /// <summary>Propagates native validation/concurrency failures as fixed unavailable state, never an accepted command.</summary>
    private static void Require(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new FactorUnavailableException();
        }
    }
}
