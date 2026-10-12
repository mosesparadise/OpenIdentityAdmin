using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Installs exactly the native verified staged key and atomically replaces obsolete local authority.</summary>
internal sealed class JourneyActivation(PlatformDbContext database, UserManager<PlatformUser> users,
    FactorAccess access, JourneyRevocation revocation)
{
    /// <summary>Runs only after the original proof budget is completed; no cookie or plaintext recovery result escapes this transaction.</summary>
    internal async Task<string[]> ActivateAsync(CeremonyTransaction transaction, CeremonyAccount account, CeremonyProof proof)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        ((AdministratorUserManager)users).RequireOperation(transaction.Token);
        var staged = proof.Staged ?? throw new FactorUnavailableException();
        if (!transaction.Plan.Governance || !string.Equals(staged.UserId, account.User.Id, StringComparison.Ordinal))
        {
            throw new FactorUnavailableException();
        }
        await ((AdministratorUserManager)users).InstallAsync(staged, transaction.Token);
        account.User.IsEnabled = true;
        account.Enrollment.State = AdministratorPolicy.Active;
        account.Enrollment.FactorGeneration = staged.Generation;
        account.Enrollment.RecoveryGeneration = checked(account.Enrollment.RecoveryGeneration + 1);
        account.Enrollment.Revision = checked(account.Enrollment.Revision + 1);
        account.Authentication.Generation = checked(account.Authentication.Generation + 1);
        await database.SaveChangesAsync(transaction.Token);
        await revocation.RevokeAsync(transaction, account);
        using var recovery = access.SelectRecovery(account.Enrollment, transaction.Token);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(account.User, RecoveryVerifier.Count)
            ?? throw new FactorUnavailableException();
        return codes.ToArray();
    }
}
