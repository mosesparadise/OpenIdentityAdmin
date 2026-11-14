using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Reuses current managed ceremony authority and independent snapshots without creating any administrator completion capability.</summary>
internal sealed class ManagedIssuanceSource(CeremonyAccount account, AdministratorInteraction ceremony)
{
    /// <summary>Acquires declared governance/admission/browser/account locks from untrusted candidates before rereading current proof.</summary>
    internal static async ValueTask<ManagedIssuanceSource> OpenAsync(ProtocolStorageScope scope, ManagedLoginInteraction row, CancellationToken token)
        => await TryOpenAsync(scope, row, observe: true, token) ?? throw ProtocolStorageOwner.Unavailable();

    /// <summary>Reads current source for issuance or UserInfo; ordinary revoked account proof returns no authority without swallowing storage faults.</summary>
    internal static async ValueTask<ManagedIssuanceSource?> TryOpenAsync(ProtocolStorageScope scope, ManagedLoginInteraction row, bool observe, CancellationToken token)
    {
        if (row.UserId is null || row.BrowserBudgetId is null || row.MfaVerifiedAt is null || row.ConsentVerifiedAt is null ||
            row.ConsentVerifiedAt < row.MfaVerifiedAt || row.ConsentVerifiedAt > ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt))
        {
            return null;
        }
        await scope.Owner.RunAsync(current => ManagedScopeReferences.VerifyBoundAsync(scope.Owner.Database, row, current), token);
        var locks = await ManagedCeremonyLocks.AcquireAsync(scope, new CeremonyLockPlan(true, true, [row.BrowserBudgetId], [row.UserId]), token);
        return await scope.Owner.RunAsync(async current =>
        {
            var database = (PlatformDbContext)scope.Owner.Database;
            await using var lease = CeremonyTransaction.BorrowManaged(locks, database, scope.Services.GetRequiredService<NativeOperation>(), current);
            var account = await CeremonyAccount.TryLoadAsync(database, lease, row.UserId);
            if (account is null || !scope.Services.GetRequiredService<ManagedAuthentication>().CanUseAccount(lease, account, row, observe))
            {
                return null;
            }
            var ceremony = await database.Set<AdministratorInteraction>().SingleOrDefaultAsync(value => value.Id == row.CeremonyId, current);
            if (ceremony is null || !ManagedAuthentication.Matches(row, ceremony) || ceremony.State != AdministratorPolicy.Consumed ||
                string.IsNullOrEmpty(await scope.Services.GetRequiredService<UserManager<PlatformUser>>().GetAuthenticatorKeyAsync(account.User)))
            {
                return null;
            }
            return new ManagedIssuanceSource(account, ceremony);
        }, token, write: true);
    }

    /// <summary>Reads only current consented identity disclosure after acquiring the original account authority.</summary>
    internal ManagedEmailProjection? Email(ManagedLoginInteraction row) => ManagedEmailProjection.Read(ManagedScopeBinding.Read(row), account.User);

    /// <summary>Freezes exact current source, ceremony, intended row and optional audit before native persistence callbacks.</summary>
    internal ManagedHumanSnapshot Snapshot(ManagedLoginInteraction row, SecurityAuditIntent? audit = null) => new(row, account, ceremony, audit);
}
