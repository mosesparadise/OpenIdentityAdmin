using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Evaluates configured survivors independently of current sessions and without reopening failure budgets.</summary>
internal sealed class GovernanceUsability(PlatformDbContext database, UserManager<PlatformUser> users, FactorAccess access, CeremonyClock clock)
{
    /// <summary>Requires native credentials, active protected material and the complete persisted platform bundle.</summary>
    internal async Task<bool> IsUsableAsync(CeremonyTransaction transaction, CeremonyAccount account, GovernanceGrantView grants,
        GovernanceRequest? projection = null) => grants.HasPlatformBundle(account.User.Id) &&
        await IsEligibleAsync(transaction, account, projection);

    /// <summary>Requires configured current credentials without conflating specialist permissions with platform survivor membership.</summary>
    internal async Task<bool> IsEligibleAsync(CeremonyTransaction transaction, CeremonyAccount account, GovernanceRequest? projection = null)
    {
        transaction.RequireGovernance();
        account.RequireCurrent(transaction);
        access.RequireActive();
        ((AdministratorUserManager)users).RequireOperation(database, transaction.Token);
        if (!IsCoherent(account, clock.Now, projection) ||
            !await users.HasPasswordAsync(account.User))
        {
            return false;
        }
        var key = await users.GetAuthenticatorKeyAsync(account.User);
        return !string.IsNullOrEmpty(key) && IsCoherent(account, clock.Now, projection);
    }

    /// <summary>Checks eligibility without changing native lockout, product failure counts or witness clock watermarks.</summary>
    private static bool IsCoherent(CeremonyAccount account, long now, GovernanceRequest? projection) => now >= 0 &&
        IsEnabled(account, projection) && !IsSuspended(account, projection) && account.User.TwoFactorEnabled &&
        account.Enrollment.State == AdministratorPolicy.Active && account.Enrollment.FactorGeneration > 0 &&
        !string.IsNullOrEmpty(account.User.SecurityStamp) && account.User.AccessFailedCount >= 0 &&
        account.Enrollment.Revision >= 0 && account.Enrollment.RecoveryGeneration >= 0 &&
        account.Authentication.Revision >= 0 && account.Authentication.LastAllocatedFactorGeneration >= account.Enrollment.FactorGeneration &&
        account.Authentication.ObservedAt is not < 0 && !(account.Authentication.ObservedAt > now + AdministratorPolicy.ClockSkewSeconds) &&
        CeremonyBudget.IsAvailable(account.User, account.Authentication, now);

    /// <summary>Projects only the explicit target's native enable flag; other commands preserve current state.</summary>
    private static bool IsEnabled(CeremonyAccount account, GovernanceRequest? projection) => projection?.UserId == account.User.Id
        ? projection.Action switch
        {
            GovernanceAction.EnableAccount => account.User.IsEnabled,
            GovernanceAction.DisableAccount => false,
            _ => account.User.IsEnabled
        } : account.User.IsEnabled;

    /// <summary>Projects only suspension; no action synthesizes active enrollment or credentials.</summary>
    private static bool IsSuspended(CeremonyAccount account, GovernanceRequest? projection) => projection?.UserId == account.User.Id
        ? projection.Action switch
        {
            GovernanceAction.SuspendAccount => true,
            GovernanceAction.ResumeAccount => account.Enrollment.Suspended,
            _ => account.Enrollment.Suspended
        } : account.Enrollment.Suspended;
}
