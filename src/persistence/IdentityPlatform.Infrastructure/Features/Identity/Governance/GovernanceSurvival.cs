using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Checks projected and actual survival against the complete locked population without suppressing mandatory security policy.</summary>
internal sealed class GovernanceSurvival(PlatformDbContext database, GovernanceGrants grants, GovernanceUsability usability)
{
    /// <summary>Rejects an ordinary command unless its projected state retains a natively usable platform administrator.</summary>
    internal async Task RequireProjectedAsync(CeremonyTransaction transaction, GovernanceState state)
    {
        transaction.RequireContext(database);
        var projected = state.Grants.Project(state.Request);
        foreach (var account in state.Accounts.Values)
        {
            if (await usability.IsUsableAsync(transaction, account, projected, state.Request))
            {
                return;
            }
        }
        throw new FactorUnavailableException();
    }

    /// <summary>Reloads real post-write state before checking both the intended effect and a locked surviving administrator.</summary>
    internal async Task RequireActualAsync(CeremonyTransaction transaction, GovernanceState state)
    {
        transaction.RequireContext(database);
        foreach (var account in state.Accounts.Values)
        {
            await database.Entry(account.User).ReloadAsync(transaction.Token);
            await database.Entry(account.Enrollment).ReloadAsync(transaction.Token);
            await database.Entry(account.Authentication).ReloadAsync(transaction.Token);
            account.RequireCurrent(transaction);
        }
        var current = await grants.ReadAsync(transaction, state.Request.RoleId);
        GovernanceEffect.Require(state, current);
        foreach (var account in state.Accounts.Values)
        {
            if (await usability.IsUsableAsync(transaction, account, current))
            {
                return;
            }
        }
        throw new FactorUnavailableException();
    }
}
