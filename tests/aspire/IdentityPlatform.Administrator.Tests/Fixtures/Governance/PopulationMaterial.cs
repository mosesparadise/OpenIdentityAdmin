using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Distinguishes missing, malformed, owner-transplanted, generation-stale and independently protected active material.</summary>
internal static class PopulationMaterial
{
    private const string CorruptCiphertext = "invalid-protected-factor";
    private const long GenerationOffset = 1;

    /// <summary>Mutates persisted active material only for its reviewed rows; unknown rows are delegated without opening a scope.</summary>
    internal static async Task<bool> TryApplyAsync(GovernanceScenario scenario, string condition)
    {
        if (condition is not (PopulationCases.MissingActiveFactor or PopulationCases.CorruptActiveFactor or
            PopulationCases.TransplantedFactor or PopulationCases.StaleFactorGeneration or PopulationCases.MissingProtectionKey))
        {
            return false;
        }
        var donor = condition == PopulationCases.TransplantedFactor ? await GovernanceAccounts.AddAsync(scenario) : null;
        await using var scope = scenario.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var factor = await database.Set<AdministratorFactor>().SingleAsync(row => row.UserId == scenario.ActorId, token);
        switch (condition)
        {
            case PopulationCases.MissingActiveFactor:
                database.Remove(factor);
                break;
            case PopulationCases.CorruptActiveFactor:
                factor.ProtectedSeed = CorruptCiphertext;
                break;
            case PopulationCases.StaleFactorGeneration:
                factor.Generation += GenerationOffset;
                break;
            case PopulationCases.TransplantedFactor:
                factor.ProtectedSeed = await database.Set<AdministratorFactor>().Where(row => row.UserId == donor)
                    .Select(row => row.ProtectedSeed).SingleAsync(token);
                break;
            case PopulationCases.MissingProtectionKey:
                ReplaceProtection(services, factor);
                break;
        }
        await database.SaveChangesAsync(token);
        return true;
    }

    /// <summary>Proves well-formed ciphertext decrypts with an independent real protection provider but not the configured provider.</summary>
    private static void ReplaceProtection(IServiceProvider services, AdministratorFactor factor)
    {
        var foreign = new FactorProtection(new EphemeralDataProtectionProvider(), services.GetRequiredService<FactorProtectionOptions>());
        factor.ProtectedSeed = foreign.Protect(FactorScenario.Seed, factor.UserId, factor.Generation, FactorProtection.Active);
        Assert.True(foreign.Unprotect(factor.ProtectedSeed, factor.UserId, factor.Generation, FactorProtection.Active) == FactorScenario.Seed);
        Assert.Throws<FactorUnavailableException>(() => services.GetRequiredService<FactorProtection>()
            .Unprotect(factor.ProtectedSeed, factor.UserId, factor.Generation, FactorProtection.Active));
    }
}
