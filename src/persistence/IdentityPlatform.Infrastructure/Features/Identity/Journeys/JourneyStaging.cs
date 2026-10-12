using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Reads bound enrollment material for display without committing GET-side authority or extending freshness.</summary>
internal sealed class JourneyStaging(PlatformDbContext database, NativeOperation operation, JourneyLocator locator,
    JourneyEligibility eligibility, CeremonyClock clock, FactorProtection protection)
{
    /// <summary>Uses rollback-only locking to prevent displaying a replaced or expired interaction's protected key.</summary>
    internal async Task<string?> ReadAsync(string publicId, string correlation, CancellationToken token)
    {
        var candidate = await locator.InteractionAsync(publicId, token);
        if (candidate is not { } target)
        {
            return null;
        }
        var plan = new CeremonyLockPlan(true, false, [target.Browser], [target.User]);
        await using var transaction = await CeremonyTransaction.OpenAsync(database, operation, plan, token);
        var account = await CeremonyAccount.LoadAsync(database, transaction, target.User);
        var interaction = await CeremonyInteraction.ReadAsync(database, transaction, account, publicId, correlation,
            AdministratorPolicy.Enrollment, clock.Now);
        if (interaction is null || !await eligibility.AllowsAsync(transaction, account, interaction) || interaction.Row.StagedSeed is null)
        {
            return null;
        }
        var seed = protection.Unprotect(interaction.Row.StagedSeed, account.User.Id, interaction.Row.DestinationGeneration,
            FactorProtection.Staged, interaction.Row.PublicId);
        if (!clock.Observe(account.Authentication, out var now) || !CeremonyBudget.IsAvailable(account.User, account.Authentication, now) ||
            !CeremonyInteraction.IsCurrent(interaction.Row, account, now))
        {
            return null;
        }
        interaction.RequireCurrent(transaction, account, now);
        return seed;
    }
}
