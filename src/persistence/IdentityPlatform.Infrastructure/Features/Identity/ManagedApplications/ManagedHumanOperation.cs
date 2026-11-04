using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Owns one managed human phase in one real protocol transaction, reusing credential primitives with execution-local borrowed ownership.</summary>
internal static class ManagedHumanOperation
{
    /// <summary>Acquires current policy/account locks, commits only exact persisted proof and withholds any result until real transaction cleanup.</summary>
    internal static async ValueTask<ManagedHumanResult> RunAsync(ProtocolStorageScope scope, ManagedHumanRequest request,
        ManagedHumanAction action, string browser, string? username, string? proof, CancellationToken token)
    {
        ManagedHumanResult result;
        await using (var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner))
        {
            var plan = await ManagedHumanCandidates.ReadAsync(scope, request, action, browser, username, token);
            var locks = await ManagedCeremonyLocks.AcquireAsync(scope, plan, token);
            var row = await ManagedHumanBinding.RowAsync(scope, request, token);
            var registration = await ManagedHumanBinding.NativeAsync(scope, request, row, token);
            registration = await registration.ObserveAsync(scope, token);
            ProtocolStorageAmbient.Current!.Request.RestrictPublication(DateTimeOffset.FromUnixTimeSeconds(Math.Min(row.ExpiresAt, registration.ExpiresAt)));
            var transition = await scope.Owner.RunAsync(current => ExecuteAsync(scope, locks, request, row, action, browser, proof, current), token, write: true);
            if (!registration.Matches(await ManagedHumanBinding.NativeAsync(scope, request, row, token)))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            await scope.Owner.RunAsync(current => new ValueTask(transition.Snapshot.VerifyAsync((PlatformDbContext)scope.Owner.Database, scope.Owner.Clock, current)), token);
            if (ManagedLoginRules.Observe(scope.Owner.Clock, Math.Max(row.ObservedAt, registration.ObservedAt)) >= Math.Min(row.ExpiresAt, registration.ExpiresAt))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            await transaction.CommitAsync(token);
            result = new(transition.Accepted, row.State, row.Revision, registration.ClientId, row.ExpiresAt, transition.NeedsDummyWork);
        }
        return result;
    }

    /// <summary>Contains every native identity operation inside one active owned execution and never returns a credential proof or borrowed adapter.</summary>
    private static async ValueTask<Transition> ExecuteAsync(ProtocolStorageScope scope, ManagedCeremonyLocks locks, ManagedHumanRequest request,
        ManagedLoginInteraction row, ManagedHumanAction action, string browser, string? proof, CancellationToken token)
    {
        var database = (PlatformDbContext)scope.Owner.Database;
        await using var ceremony = CeremonyTransaction.BorrowManaged(locks, database, scope.Services.GetRequiredService<NativeOperation>(), token);
        var account = locks.Plan.Users.Count == 0 ? null : await CeremonyAccount.LoadAsync(database, ceremony, locks.Plan.Users.Single());
        var authentication = scope.Services.GetRequiredService<ManagedAuthentication>();
        var invariant = new ManagedHumanInvariant(row, account);
        if (row.UserId is not null)
        {
            if (account is null)
            {
                throw new FactorUnavailableException();
            }
            authentication.RequireAccount(ceremony, account, row);
            if (string.IsNullOrEmpty(await scope.Services.GetRequiredService<UserManager<PlatformUser>>().GetAuthenticatorKeyAsync(account.User)))
            {
                throw new FactorUnavailableException();
            }
        }
        if (row.CeremonyId is not null)
        {
            var bound = await database.Set<AdministratorInteraction>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == row.CeremonyId, ceremony.Token);
            if (bound is null || !ManagedAuthentication.Matches(row, bound) || bound.State != (row.State == ManagedLoginState.PendingMfa
                ? IdentityPlatform.Core.Features.Identity.Authentication.AdministratorPolicy.Pending : IdentityPlatform.Core.Features.Identity.Authentication.AdministratorPolicy.Consumed))
            {
                throw new FactorUnavailableException();
            }
        }
        var dummy = action == ManagedHumanAction.Password && account is null && await scope.Services.GetRequiredService<CeremonyAdmission>().CanAttemptAsync(ceremony, browser);
        var accepted = await ApplyAsync(authentication, ceremony, account, request, row, action, browser, proof, ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt));
        await invariant.RequireAsync(database, action, accepted, ceremony, authentication.PasswordUpdate, ceremony.Token);
        row.ObservedAt = ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt);
        if (accepted && action != ManagedHumanAction.Read)
        {
            row.Revision = checked(row.Revision + 1);
        }
        var audit = ManagedHumanAudit.Add(database, row, action, account?.User.Id, accepted);
        var stored = row.CeremonyId is null ? null : await database.Set<AdministratorInteraction>().SingleAsync(value => value.Id == row.CeremonyId, ceremony.Token);
        if (stored is not null && !ManagedAuthentication.Matches(row, stored))
        {
            throw new FactorUnavailableException();
        }
        var snapshot = new ManagedHumanSnapshot(row, account, stored, audit);
        await database.SaveChangesAsync(ceremony.Token);
        await snapshot.VerifyAsync(database, scope.Owner.Clock, ceremony.Token);
        return new(accepted, snapshot, dummy || authentication.NeedsDummyWork);
    }

    /// <summary>Allows only the expected next phase; terminal/duplicate submissions never reopen proof or reset the human deadline.</summary>
    private static async Task<bool> ApplyAsync(ManagedAuthentication authentication, CeremonyTransaction ceremony, CeremonyAccount? account,
        ManagedHumanRequest request, ManagedLoginInteraction row, ManagedHumanAction action, string browser, string? proof, long now)
    {
        if (action == ManagedHumanAction.Read)
        {
            return true;
        }
        if (action == ManagedHumanAction.Password && row.State == ManagedLoginState.PendingPassword)
        {
            return account is not null && await authentication.PasswordAsync(ceremony, account, row, browser, request.Correlation, proof!);
        }
        if (action == ManagedHumanAction.Mfa && row.State == ManagedLoginState.PendingMfa)
        {
            return account is not null && await authentication.MfaAsync(ceremony, account, row, request.Correlation, proof!);
        }
        if (row.State == ManagedLoginState.PendingConsent && action is ManagedHumanAction.Approve or ManagedHumanAction.Deny)
        {
            if (account is null || row.MfaVerifiedAt is null)
            {
                throw new FactorUnavailableException();
            }
            authentication.RequireAccount(ceremony, account, row);
            row.State = action == ManagedHumanAction.Approve ? ManagedLoginState.Approved : ManagedLoginState.Denied;
            row.ConsentVerifiedAt = row.ObservedAt = Math.Max(row.ObservedAt, now);
            return true;
        }
        throw new FactorUnavailableException();
    }

    /// <summary>Retains only a final-state verification handle inside the coordinator; no mutable entity escapes to account pages.</summary>
    private sealed record Transition(bool Accepted, ManagedHumanSnapshot Snapshot, bool NeedsDummyWork);
}
