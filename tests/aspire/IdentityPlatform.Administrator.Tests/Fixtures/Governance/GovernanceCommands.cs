using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Prepares independently controlled native targets for the fixed command catalogue and its no-op controls.</summary>
internal static class GovernanceCommands
{
    internal const string Assign = "assign-bundle";
    internal const string Remove = "remove-bundle";
    internal const string RemoveGrant = "remove-specialist-grant";
    internal const string RestoreGrant = "restore-specialist-grant";
    internal const string Disable = "disable-account";
    internal const string Enable = "enable-account";
    internal const string Suspend = "suspend-account";
    internal const string Resume = "resume-account";
    internal static readonly string[] Cases = [Assign, Remove, RemoveGrant, RestoreGrant, Disable, Enable, Suspend, Resume];
    private const string Platform = "PlatformAdministrator";
    private const string Specialist = "IdentityAdministrator";
    private const string PermissionType = "identityplatform.permission";
    internal const string Permission = "users.read";

    /// <summary>Creates finite target intent with actual stored versions, using real usable witnesses for destructive self-changes.</summary>
    internal static async Task<GovernanceRequest> PrepareAsync(GovernanceScenario scenario, string name, bool noChange = false)
    {
        var action = Action(name);
        if (action is GovernanceAction.AddGrant or GovernanceAction.RemoveGrant)
        {
            var role = await RoleAsync(scenario, Specialist);
            if ((action == GovernanceAction.AddGrant) != noChange)
            {
                await RemoveClaimAsync(scenario, role);
            }
            return await scenario.RequestAsync(action, roleId: role, permission: Permission);
        }
        var target = await TargetAsync(scenario, action, noChange);
        var roleId = action is GovernanceAction.AssignBundle or GovernanceAction.RemoveBundle ? await RoleAsync(scenario, Platform) : null;
        return await scenario.RequestAsync(action, target, roleId);
    }

    /// <summary>Separates nominal account setup from the production transition being tested.</summary>
    private static async Task<string> TargetAsync(GovernanceScenario scenario, GovernanceAction action, bool noChange)
    {
        if (action == GovernanceAction.AssignBundle)
        {
            return noChange ? scenario.ActorId : await GovernanceAccounts.AddAsync(scenario, false, false);
        }
        if (action == GovernanceAction.RemoveBundle)
        {
            if (noChange)
            {
                return await GovernanceAccounts.AddAsync(scenario, false, false);
            }
            _ = await GovernanceAccounts.AddAsync(scenario);
            return scenario.ActorId;
        }
        if (action is GovernanceAction.DisableAccount or GovernanceAction.SuspendAccount && !noChange)
        {
            _ = await GovernanceAccounts.AddAsync(scenario);
            return scenario.ActorId;
        }
        if (action is GovernanceAction.EnableAccount or GovernanceAction.ResumeAccount && noChange)
        {
            return scenario.ActorId;
        }
        var target = await GovernanceAccounts.AddAsync(scenario);
        await SetInactiveAsync(scenario, target, action is GovernanceAction.EnableAccount or GovernanceAction.DisableAccount);
        return target;
    }

    /// <summary>Sets exactly the state needed for enable/resume or an already-disabled/suspended no-op.</summary>
    private static async Task SetInactiveAsync(GovernanceScenario scenario, string target, bool disable)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        if (disable)
        {
            Assert.Equal(1, await database.Users.Where(row => row.Id == target).ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, false), token));
        }
        else
        {
            Assert.Equal(1, await database.Set<AdministratorEnrollment>().Where(row => row.UserId == target)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Suspended, true), token));
        }
    }

    /// <summary>Resolves an independently named native role without importing production catalogue metadata.</summary>
    internal static async Task<string> RoleAsync(GovernanceScenario scenario, string name)
    {
        await using var scope = scenario.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Roles.Where(row => row.Name == name)
            .Select(row => row.Id).SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Removes one actual native claim for a restore transition or already-absent no-op.</summary>
    private static async Task RemoveClaimAsync(GovernanceScenario scenario, string role)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.RoleClaims.Where(row => row.RoleId == role && row.ClaimType == PermissionType && row.ClaimValue == Permission)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Maps fixed test identities to requested actions independently of the production dispatch implementation.</summary>
    internal static GovernanceAction Action(string name) => name switch
    {
        Assign => GovernanceAction.AssignBundle,
        Remove => GovernanceAction.RemoveBundle,
        RestoreGrant => GovernanceAction.AddGrant,
        RemoveGrant => GovernanceAction.RemoveGrant,
        Disable => GovernanceAction.DisableAccount,
        Enable => GovernanceAction.EnableAccount,
        Suspend => GovernanceAction.SuspendAccount,
        Resume => GovernanceAction.ResumeAccount,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
}
