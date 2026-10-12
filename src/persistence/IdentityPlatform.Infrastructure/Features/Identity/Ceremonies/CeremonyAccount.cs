using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Holds freshly read state exclusively owned by one locked ceremony transaction.</summary>
internal sealed class CeremonyAccount
{
    private readonly CeremonyTransaction owner;
    private readonly PlatformDbContext database;
    private readonly string userId;
    internal PlatformUser User
    {
        get;
    }
    internal AdministratorEnrollment Enrollment
    {
        get;
    }
    internal AdministratorAuthentication Authentication
    {
        get;
    }

    /// <summary>Captures only native state loaded after the original transaction acquired its declared account lock.</summary>
    private CeremonyAccount(PlatformDbContext database, CeremonyTransaction owner, PlatformUser user,
        AdministratorEnrollment enrollment, AdministratorAuthentication authentication)
    {
        this.database = database;
        this.owner = owner;
        User = user;
        userId = user.Id;
        Enrollment = enrollment;
        Authentication = authentication;
    }

    /// <summary>Rejects cross-transaction, detached and mixed-owner state before any policy operation.</summary>
    internal void RequireCurrent(CeremonyTransaction transaction)
    {
        transaction.RequireContext(database);
        transaction.RequireCeremony();
        if (!ReferenceEquals(transaction, owner) || User.Id != userId || !transaction.Plan.Users.Contains(userId, StringComparer.Ordinal) ||
            Enrollment.UserId != User.Id || Authentication.UserId != User.Id ||
            database.Entry(User).State == EntityState.Detached || database.Entry(Enrollment).State == EntityState.Detached ||
            database.Entry(Authentication).State == EntityState.Detached)
        {
            throw new FactorUnavailableException();
        }
    }
    /// <summary>Reloads native and product state only after account serialization, rejecting provider padding aliases.</summary>
    internal static async Task<CeremonyAccount> LoadAsync(PlatformDbContext database, CeremonyTransaction transaction,
        string userId)
    {
        transaction.RequireContext(database);
        transaction.RequireCeremony();
        if (!transaction.Plan.Users.Contains(userId, StringComparer.Ordinal))
        {
            throw new FactorUnavailableException();
        }
        var user = await database.Users.SingleOrDefaultAsync(row => row.Id == userId, transaction.Token);
        var enrollment = await database.Set<AdministratorEnrollment>().SingleOrDefaultAsync(row => row.UserId == userId, transaction.Token);
        var authentication = await database.Set<AdministratorAuthentication>().SingleOrDefaultAsync(row => row.UserId == userId, transaction.Token);
        if (user is null || enrollment is null || authentication is null || user.Id != userId ||
            enrollment.UserId != userId || authentication.UserId != userId || string.IsNullOrEmpty(user.SecurityStamp))
        {
            throw new FactorUnavailableException();
        }
        return new CeremonyAccount(database, transaction, user, enrollment, authentication);
    }

    /// <summary>Requires current enabled active state and a positive enrolled factor; managers still verify protected material.</summary>
    internal bool IsActive => User.IsEnabled && User.TwoFactorEnabled && !Enrollment.Suspended &&
        Enrollment.State == AdministratorPolicy.Active && Enrollment.FactorGeneration > 0;
}

/// <summary>Maintains a monotonic durable clock fence independently of the native provider's wall clock.</summary>
internal sealed class CeremonyClock(TimeProvider time)
{
    internal long Now => time.GetUtcNow().ToUnixTimeSeconds();

    /// <summary>Advances the watermark only on a safe observation; excessive rollback never permits pruning or proof acceptance.</summary>
    internal bool Observe(AdministratorAuthentication state) => Observe(state, out _);

    /// <summary>Returns the exact accepted sample so consumers never prune or timestamp against an unrecorded clock read.</summary>
    internal bool Observe(AdministratorAuthentication state, out long now)
    {
        now = Now;
        if (now < 0 || state.ObservedAt is < 0 || state.ObservedAt > now + AdministratorPolicy.ClockSkewSeconds)
        {
            return false;
        }
        state.ObservedAt = Math.Max(state.ObservedAt ?? now, now);
        return true;
    }
}

/// <summary>Uses the shared durable failure count and checked native lockout operations under the account lock.</summary>
internal sealed class CeremonyBudget(UserManager<PlatformUser> users, CeremonyClock clock)
{
    /// <summary>Checks current native and product eligibility without reopening expired budgets or changing counters.</summary>
    internal static bool IsAvailable(PlatformUser user, AdministratorAuthentication state, long now) =>
        user.LockoutEnabled && state.Generation > 0 && state.Failures is >= 0 and <= AdministratorPolicy.FailedAttempts &&
        state.LockoutUntil is not < 0 &&
        (state.Failures == AdministratorPolicy.FailedAttempts) == (state.LockoutUntil is not null) &&
        !(state.LockoutUntil > now) && !(user.LockoutEnd > DateTimeOffset.FromUnixTimeSeconds(now));

    /// <summary>Reopens only a legitimately expired lockout; ordinary password success cannot reset factor failures.</summary>
    internal async Task<bool> OpenAsync(CeremonyTransaction transaction, CeremonyAccount account)
    {
        account.RequireCurrent(transaction);
        ((AdministratorUserManager)users).RequireOperation(transaction.Token);
        var state = account.Authentication;
        if (!clock.Observe(state, out var now) || !IsAvailable(account.User, state, now))
        {
            return false;
        }
        if (state.LockoutUntil is not null)
        {
            Require(await users.SetLockoutEndDateAsync(account.User, null));
            Require(await users.ResetAccessFailedCountAsync(account.User));
            state.Failures = 0;
            state.LockoutUntil = null;
        }
        return true;
    }

    /// <summary>Records a bound failed submission; its caller must commit a denial audit in the same transaction.</summary>
    internal async Task FailAsync(CeremonyTransaction transaction, CeremonyAccount account, CeremonyInteraction? binding = null)
    {
        account.RequireCurrent(transaction);
        ((AdministratorUserManager)users).RequireOperation(transaction.Token);
        if (!await OpenAsync(transaction, account))
        {
            throw new FactorUnavailableException();
        }
        if (binding is not null)
        {
            binding.RequireCurrent(transaction, account, clock.Now);
            var interaction = binding.Row;
            if (interaction.UserId != account.User.Id || interaction.State != AdministratorPolicy.Pending ||
                interaction.Attempts >= AdministratorPolicy.FailedAttempts)
            {
                throw new FactorUnavailableException();
            }
            interaction.Attempts++;
            if (interaction.Attempts == AdministratorPolicy.FailedAttempts)
            {
                interaction.State = AdministratorPolicy.Consumed;
            }
        }
        account.Authentication.Failures++;
        if (account.Authentication.Failures == AdministratorPolicy.FailedAttempts)
        {
            account.Authentication.LockoutUntil = clock.Now + AdministratorPolicy.LockoutSeconds;
            CeremonyMetrics.Record(CeremonyOutcome.Locked);
            Require(await users.SetLockoutEndDateAsync(account.User, DateTimeOffset.FromUnixTimeSeconds(account.Authentication.LockoutUntil.Value)));
        }
    }

    /// <summary>Clears failures only after a verified current factor proof, never after password-only verification.</summary>
    internal async Task CompleteAsync(CeremonyTransaction transaction, CeremonyAccount account, CeremonyProof proof)
    {
        ((AdministratorUserManager)users).RequireOperation(transaction.Token);
        proof.Consume(transaction, account, clock.Now);
        Require(await users.ResetAccessFailedCountAsync(account.User));
        account.Authentication.Failures = 0;
        account.Authentication.LockoutUntil = null;
    }

    /// <summary>Clears only a completed restricted recovery budget; this capability cannot authorize a full session.</summary>
    internal async Task CompleteRecoveryAsync(CeremonyTransaction transaction, CeremonyAccount account, CeremonyRecoveryProof proof)
    {
        ((AdministratorUserManager)users).RequireOperation(transaction.Token);
        proof.Consume(transaction, account, clock.Now);
        Require(await users.ResetAccessFailedCountAsync(account.User));
        account.Authentication.Failures = 0;
        account.Authentication.LockoutUntil = null;
    }

    /// <summary>Rejects native concurrency or persistence failures without converting them into successful state changes.</summary>
    private static void Require(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new FactorUnavailableException();
        }
    }
}
