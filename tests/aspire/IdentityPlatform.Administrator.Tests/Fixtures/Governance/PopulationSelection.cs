using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Creates test-owned persisted material and enters real transaction-bound selections without constructing private proof objects.</summary>
internal static class PopulationSelection
{
    private const string Pending = "Pending";
    private const string Replacement = "Replacement";
    private const int InteractionLifetimeSeconds = 300;
    private const long NextGeneration = 1;

    /// <summary>Creates coherent replacement staging bound to the actual native owner; this fixture does not claim shipped onboarding or ceremony authority.</summary>
    internal static async Task<Guid> StageAsync(GovernanceScenario scenario)
    {
        await using var scope = scenario.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var user = await database.Users.SingleAsync(row => row.Id == scenario.ActorId, token);
        var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(row => row.UserId == user.Id, token);
        var authentication = await database.Set<AdministratorAuthentication>().SingleAsync(row => row.UserId == user.Id, token);
        authentication.LastAllocatedFactorGeneration += NextGeneration;
        var identifier = CeremonySecrets.Create();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var interaction = new AdministratorInteraction
        {
            UserId = user.Id,
            PublicId = identifier,
            BrowserBudgetId = identifier,
            BrowserDigest = identifier,
            Purpose = Replacement,
            State = Pending,
            SecurityStamp = user.SecurityStamp!,
            Generation = authentication.Generation,
            DestinationGeneration = authentication.LastAllocatedFactorGeneration,
            ExpectedEnrollmentRevision = enrollment.Revision,
            CreatedAt = now,
            ExpiresAt = now + InteractionLifetimeSeconds,
            PasswordVerifiedAt = now,
            StagedSeed = services.GetRequiredService<FactorProtection>().Protect(FactorScenario.Seed, user.Id,
                authentication.LastAllocatedFactorGeneration, FactorProtection.Staged, identifier)
        };
        database.Add(new AdministratorBrowserBudget { Id = identifier, ExpiresAt = interaction.ExpiresAt });
        database.Add(interaction);
        await database.SaveChangesAsync(token);
        return interaction.Id;
    }

    /// <summary>Returns only real selection leases; installation uses native TOTP verification through the private factory.</summary>
    internal static async Task<IDisposable> EnterAsync(IServiceProvider services, string userId, Guid interactionId, string selection)
    {
        var database = services.GetRequiredService<PlatformDbContext>();
        var access = services.GetRequiredService<FactorAccess>();
        var token = TestContext.Current.CancellationToken;
        if (selection == PopulationCases.Recovery)
        {
            var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(row => row.UserId == userId, token);
            Assert.True(enrollment.RecoveryGeneration > 0);
            return access.SelectRecovery(enrollment, token);
        }
        var interaction = await database.Set<AdministratorInteraction>().SingleAsync(row => row.Id == interactionId, token);
        if (selection == PopulationCases.Staged)
        {
            return access.Select(interaction, token);
        }
        Assert.Equal(PopulationCases.Installation, selection);
        var proof = await VerifiedStagedFactor.VerifyAsync(database, access, services.GetRequiredService<UserManager<PlatformUser>>(),
            TimeProvider.System, interactionId, FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), token);
        Assert.NotNull(proof);
        return access.Install(proof, token);
    }

    /// <summary>Proves selected staging is cryptographically valid and accepted by the actual native authenticator before governance refuses it.</summary>
    internal static async Task RequireValidStagingAsync(IServiceProvider services, string userId)
    {
        var users = services.GetRequiredService<UserManager<PlatformUser>>();
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        Assert.True(await users.GetAuthenticatorKeyAsync(user) == FactorScenario.Seed);
        Assert.True(await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider,
            FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds())));
    }
}
