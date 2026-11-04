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
    {
        if (row.UserId is null || row.BrowserBudgetId is null || row.MfaVerifiedAt is null || row.ConsentVerifiedAt is null ||
            row.ConsentVerifiedAt < row.MfaVerifiedAt || row.ConsentVerifiedAt > ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var locks = await ManagedCeremonyLocks.AcquireAsync(scope, new CeremonyLockPlan(true, true, [row.BrowserBudgetId], [row.UserId]), token);
        return await scope.Owner.RunAsync(async current =>
        {
            var database = (PlatformDbContext)scope.Owner.Database;
            await using var lease = CeremonyTransaction.BorrowManaged(locks, database, scope.Services.GetRequiredService<NativeOperation>(), current);
            var account = await CeremonyAccount.LoadAsync(database, lease, row.UserId);
            scope.Services.GetRequiredService<ManagedAuthentication>().RequireAccount(lease, account, row);
            var ceremony = await database.Set<AdministratorInteraction>().SingleOrDefaultAsync(value => value.Id == row.CeremonyId, current);
            if (ceremony is null || !ManagedAuthentication.Matches(row, ceremony) || ceremony.State != AdministratorPolicy.Consumed ||
                string.IsNullOrEmpty(await scope.Services.GetRequiredService<UserManager<PlatformUser>>().GetAuthenticatorKeyAsync(account.User)))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return new ManagedIssuanceSource(account, ceremony);
        }, token, write: true);
    }

    /// <summary>Freezes exact current source, ceremony, intended row and optional audit before native persistence callbacks.</summary>
    internal ManagedHumanSnapshot Snapshot(ManagedLoginInteraction row, SecurityAuditIntent? audit = null) => new(row, account, ceremony, audit);
}
