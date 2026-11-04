using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Observes an actual native password upgrade; it permits only the exact successful native-generated hash/stamp successor.</summary>
internal sealed class ManagedPasswordUpdate
{
    private readonly CeremonyTransaction transaction;
    private readonly CeremonyAccount account;
    private readonly string? originalHash;
    private readonly string? originalStamp;
    private PasswordVerificationResult? verified;
    private bool updating;
    private bool updated;
    private bool completed;
    internal string? Hash
    {
        get; private set;
    }
    internal string? Stamp
    {
        get; private set;
    }

    /// <summary>Captures the original bound account before any native password verification or save callback runs.</summary>
    private ManagedPasswordUpdate(CeremonyTransaction transaction, CeremonyAccount account)
    {
        this.transaction = transaction;
        this.account = account;
        originalHash = Hash = account.User.PasswordHash;
        originalStamp = Stamp = account.User.SecurityStamp;
    }

    /// <summary>Creates a receipt only by running the genuine native ceremony password path under its exact original operation.</summary>
    internal static async Task<(CeremonyPassword? Proof, ManagedPasswordUpdate? Update)> VerifyAsync(UserManager<PlatformUser> users,
        CeremonyBudget budget, CeremonyClock clock, CeremonyTransaction transaction, CeremonyAccount account, string password)
    {
        account.RequireCurrent(transaction);
        account.RequireManager(users, transaction);
        var observation = new ManagedPasswordUpdate(transaction, account);
        var proof = await ((AdministratorUserManager)users).ObservePasswordAsync(observation,
            () => CeremonyPassword.VerifyAsync(users, budget, clock, transaction, account, ManagedAuthentication.Purpose, password));
        if (proof is null)
        {
            return (null, null);
        }
        observation.RequireValues(account.User);
        if (observation.verified is not (PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded) ||
            observation.updated != (observation.verified == PasswordVerificationResult.SuccessRehashNeeded) || observation.updating)
        {
            throw new FactorUnavailableException();
        }
        observation.completed = true;
        return (proof, observation);
    }

    /// <summary>Accepts exactly one actual native verifier result while the original hash and stamp still match.</summary>
    internal void Verified(PlatformUser user, PasswordVerificationResult result)
    {
        RequireValues(user);
        if (verified is not null || completed)
        {
            throw new FactorUnavailableException();
        }
        verified = result;
    }

    /// <summary>Captures only an upgrade requested by the actual native verifier, before any native persistence callback can change its values.</summary>
    internal void BeforeUpdate(PlatformUser user)
    {
        RequireAccount(user);
        if (verified != PasswordVerificationResult.SuccessRehashNeeded || updating || updated || completed ||
            string.IsNullOrEmpty(user.PasswordHash) || user.PasswordHash == originalHash || string.IsNullOrEmpty(user.SecurityStamp) || user.SecurityStamp == originalStamp)
        {
            throw new FactorUnavailableException();
        }
        Hash = user.PasswordHash;
        Stamp = user.SecurityStamp;
        updating = true;
    }

    /// <summary>Identity discards the update result in CheckPasswordAsync, so the receipt explicitly requires successful exact persistence acknowledgement.</summary>
    internal void AfterUpdate(PlatformUser user, IdentityResult result)
    {
        RequireValues(user);
        if (!updating || !result.Succeeded || updated || completed)
        {
            throw new FactorUnavailableException();
        }
        updating = false;
        updated = true;
    }

    /// <summary>Rejects transplanted or unfinished receipts before the invariant accepts the native successor pair.</summary>
    internal void Require(CeremonyTransaction current, CeremonyAccount candidate, string? hash, string? stamp)
    {
        RequireValues(candidate.User);
        if (!completed || !ReferenceEquals(current, transaction) || !ReferenceEquals(candidate, account) || hash != originalHash || stamp != originalStamp)
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Requires exact native entity and current transaction ownership before observing or consuming an upgrade.</summary>
    private void RequireAccount(PlatformUser user)
    {
        account.RequireCurrent(transaction);
        if (!ReferenceEquals(user, account.User))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Checks both the original pair and the frozen successor, including tracked callback mutations.</summary>
    private void RequireValues(PlatformUser user)
    {
        RequireAccount(user);
        if (user.PasswordHash != Hash || user.SecurityStamp != Stamp)
        {
            throw new FactorUnavailableException();
        }
    }
}
