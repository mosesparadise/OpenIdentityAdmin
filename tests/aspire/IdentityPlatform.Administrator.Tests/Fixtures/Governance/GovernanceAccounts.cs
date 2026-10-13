using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Configures explicit test witnesses with native credentials; it does not implement product onboarding.</summary>
internal static class GovernanceAccounts
{
    private const string PlatformRole = "PlatformAdministrator";
    private const string Active = "Active";
    private const string Pending = "Pending";
    private const string Enrollment = "Enrollment";
    private const string Login = "Login";
    private const int InteractionSeconds = 300;
    private const int FirstGeneration = 1;
    private const string NativePasswordSuffix = "!";

    /// <summary>Returns the shared synthetic witness credential convention without duplicating policy literals across tests.</summary>
    internal static string Password(GovernanceScenario scenario) => Password(scenario.Password);

    /// <summary>Returns the same synthetic witness convention for native and HTTP-backed test scopes.</summary>
    internal static string Password(string originalPassword) => originalPassword + NativePasswordSuffix;

    /// <summary>Creates a native nominee and coherent product rows without rewriting the permanent bootstrap receipt.</summary>
    internal static Task<string> AddAsync(GovernanceScenario scenario, bool usable = true, bool platformMember = true, string? userId = null) =>
        AddAsync(scenario.CreateScope, Password(scenario), usable, platformMember, userId);

    /// <summary>Reuses native witness provisioning for a caller-owned real database and material scope without owning its environment.</summary>
    internal static async Task<string> AddAsync(Func<AsyncServiceScope> createScope, string password, bool usable = true, bool platformMember = true, string? userId = null)
    {
        await using var scope = createScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var users = services.GetRequiredService<UserManager<PlatformUser>>();
        var user = new PlatformUser { UserName = Guid.NewGuid().ToString(), IsEnabled = usable, LockoutEnabled = true };
        if (userId is not null)
        {
            user.Id = userId;
        }
        var created = usable ? await users.CreateAsync(user, password) : await users.CreateAsync(user);
        Assert.True(created.Succeeded);
        var enrollment = new AdministratorEnrollment { UserId = user.Id };
        database.Add(enrollment);
        database.Add(new AdministratorAuthentication { UserId = user.Id, LastAllocatedFactorGeneration = FirstGeneration });
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        if (platformMember)
        {
            Assert.True((await users.AddToRoleAsync(user, PlatformRole)).Succeeded);
        }
        if (usable)
        {
            await InstallAsync(services, user, enrollment);
            Assert.True(await users.HasPasswordAsync(user));
            Assert.True(user.TwoFactorEnabled);
            Assert.True(await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider,
                FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds())));
        }
        return user.Id;
    }

    /// <summary>Uses real staged native verification and the private installation capability to establish protected active material.</summary>
    private static async Task InstallAsync(IServiceProvider services, PlatformUser user, AdministratorEnrollment enrollment)
    {
        var database = services.GetRequiredService<PlatformDbContext>();
        var users = services.GetRequiredService<UserManager<PlatformUser>>();
        var token = TestContext.Current.CancellationToken;
        var interaction = await StageAsync(services, user);
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var code = FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var proof = await VerifiedStagedFactor.VerifyAsync(database, services.GetRequiredService<FactorAccess>(), users,
            TimeProvider.System, interaction.Id, code, token);
        Assert.NotNull(proof);
        await Assert.IsType<AdministratorUserManager>(users).InstallAsync(proof, token);
        enrollment.State = Active;
        enrollment.FactorGeneration = FirstGeneration;
        enrollment.Revision = FirstGeneration;
        interaction.State = IdentityPlatform.Core.Features.Identity.Authentication.AdministratorPolicy.Consumed;
        interaction.StagedSeed = null;
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    /// <summary>Stores synthetic enrollment staging bound to the new native user; it grants no session or command authority.</summary>
    private static async Task<AdministratorInteraction> StageAsync(IServiceProvider services, PlatformUser user)
    {
        var database = services.GetRequiredService<PlatformDbContext>();
        var protection = services.GetRequiredService<FactorProtection>();
        var identifier = CeremonySecrets.Create();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        database.Add(new AdministratorBrowserBudget { Id = identifier, ExpiresAt = now + InteractionSeconds });
        var interaction = new AdministratorInteraction
        {
            UserId = user.Id,
            PublicId = identifier,
            BrowserBudgetId = identifier,
            BrowserDigest = identifier,
            Purpose = Enrollment,
            State = Pending,
            SecurityStamp = user.SecurityStamp!,
            CreatedAt = now,
            ExpiresAt = now + InteractionSeconds,
            PasswordVerifiedAt = now,
            StagedSeed = protection.Protect(FactorScenario.Seed, user.Id, FirstGeneration, FactorProtection.Staged, identifier)
        };
        database.Add(interaction);
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        return interaction;
    }

    /// <summary>Obtains a configured witness's full session through actual password and native TOTP login.</summary>
    internal static Task<JourneySessionCandidate> LoginAsync(GovernanceScenario scenario, string userId) =>
        LoginAsync(scenario.CreateScope, Password(scenario), userId);

    /// <summary>Runs real native password/TOTP login using the same existing scope owner and explicit synthetic witness credential.</summary>
    internal static async Task<JourneySessionCandidate> LoginAsync(Func<AsyncServiceScope> createScope, string password, string userId)
    {
        await using var scope = createScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var name = await database.Users.Where(row => row.Id == userId).Select(row => row.UserName).SingleAsync(token);
        var correlation = CeremonySecrets.Create();
        var pending = await services.GetRequiredService<JourneyPassword>().BeginAsync(name!, password, Login,
            CeremonySecrets.Create(), correlation, token);
        Assert.NotNull(pending.Interaction);
        var grant = await services.GetRequiredService<JourneyCompletion>().CompleteAsync(pending.Interaction, correlation, Login,
            FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), token);
        Assert.NotNull(grant);
        return new JourneySessionCandidate(grant.UserId, grant.SessionId);
    }
}
