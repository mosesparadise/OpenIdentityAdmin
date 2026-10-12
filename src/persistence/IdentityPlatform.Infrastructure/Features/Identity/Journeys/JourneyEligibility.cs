using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Distinguishes the permanent bootstrap exception from enabled administrator authentication.</summary>
internal sealed class JourneyEligibility(PlatformDbContext database)
{
    private const int Singleton = 1;

    /// <summary>Rereads ownership under governance serialization; no native enable flag is changed to check a password.</summary>
    internal async Task<bool> AllowsAsync(CeremonyTransaction transaction, CeremonyAccount account, string purpose)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        if (purpose is AdministratorPolicy.Login or AdministratorPolicy.Replacement or AdministratorPolicy.Regeneration)
        {
            return account.IsActive;
        }
        if (purpose == AdministratorPolicy.Recovery)
        {
            return account.User.IsEnabled && account.User.TwoFactorEnabled && !account.Enrollment.Suspended &&
                account.Enrollment.State is AdministratorPolicy.Active or AdministratorPolicy.RecoveryRequired &&
                account.Enrollment.FactorGeneration > 0 && account.Enrollment.RecoveryGeneration > 0;
        }
        if (purpose != AdministratorPolicy.Enrollment || !transaction.Plan.Governance || account.User.IsEnabled ||
            account.User.TwoFactorEnabled || account.Enrollment.Suspended || account.Enrollment.State != AdministratorPolicy.Pending ||
            account.Enrollment.FactorGeneration != 0 || account.Enrollment.RecoveryGeneration != 0)
        {
            return false;
        }
        var receipt = await database.Set<AdministratorBootstrapReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == Singleton, transaction.Token);
        var governance = await database.Set<AdministratorGovernance>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == Singleton, transaction.Token);
        return governance?.BootstrapConsumed == true && receipt?.UserId == account.User.Id &&
            !await database.Set<AdministratorFactor>().AnyAsync(row => row.UserId == account.User.Id, transaction.Token);
    }

    /// <summary>Rejects legacy, stale revision or transplanted staged destinations before completion or secret display.</summary>
    internal async Task<bool> AllowsAsync(CeremonyTransaction transaction, CeremonyAccount account, CeremonyInteraction interaction)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        var row = interaction.Row;
        if (row.Purpose == AdministratorPolicy.Replacement && row.AuthorizationKind is not null)
        {
            return JourneyAuthorization.IsStaged(account, row);
        }
        return row.ExpectedEnrollmentRevision == account.Enrollment.Revision &&
            (row.Purpose == AdministratorPolicy.Enrollment
                ? row.DestinationGeneration <= account.Authentication.LastAllocatedFactorGeneration
                : row.DestinationGeneration == account.Enrollment.FactorGeneration) &&
            await AllowsAsync(transaction, account, row.Purpose);
    }
}
