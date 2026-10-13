using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Creates explicit retained-storage occupancy without issuing synthetic cookies or granting client authority.</summary>
internal static class SecurityCapacity
{
    private const int RetainedSessionLimit = 32;
    private const int RetainedInteractionLimit = 10000;
    private const string Consumed = "Consumed";

    /// <summary>Fills retained user capacity with revoked rows while preserving the real initiating session.</summary>
    internal static async Task FillSessionsAsync(JourneyScenario scenario, int count = RetainedSessionLimit, bool revoked = true)
    {
        var token = TestContext.Current.CancellationToken;
        var original = await scenario.Database.Set<AdministratorSession>().AsNoTracking().SingleAsync(token);
        for (var index = 1; index < count; index++)
        {
            scenario.Database.Add(new AdministratorSession
            {
                PublicId = CeremonySecrets.Create(),
                UserId = original.UserId,
                SecurityStamp = original.SecurityStamp,
                AuthenticationMethods = original.AuthenticationMethods,
                Generation = original.Generation,
                FactorGeneration = original.FactorGeneration,
                PasswordVerifiedAt = original.PasswordVerifiedAt,
                FactorVerifiedAt = original.FactorVerifiedAt,
                CreatedAt = original.CreatedAt,
                LastActivityAt = original.LastActivityAt,
                ExpiresAt = original.ExpiresAt,
                Revoked = revoked
            });
        }
        await scenario.Database.SaveChangesAsync(token);
    }

    /// <summary>Fills physical interaction capacity with consumed rows, retaining the existing live slot and its original browser budget.</summary>
    internal static async Task FillInteractionsAsync(JourneyScenario scenario, string publicId)
    {
        var token = TestContext.Current.CancellationToken;
        var original = await SecurityScenario.InteractionAsync(scenario, publicId);
        var count = await scenario.Database.Set<AdministratorInteraction>().CountAsync(token);
        for (var index = count; index < RetainedInteractionLimit; index++)
        {
            scenario.Database.Add(new AdministratorInteraction
            {
                PublicId = CeremonySecrets.Create(),
                UserId = original.UserId,
                BrowserBudgetId = original.BrowserBudgetId,
                BrowserDigest = original.BrowserDigest,
                Purpose = original.Purpose,
                State = Consumed,
                SecurityStamp = original.SecurityStamp,
                Generation = original.Generation,
                DestinationGeneration = original.DestinationGeneration,
                CreatedAt = original.CreatedAt,
                ExpiresAt = original.ExpiresAt,
                PasswordVerifiedAt = original.PasswordVerifiedAt
            });
        }
        await scenario.Database.SaveChangesAsync(token);
    }
}
