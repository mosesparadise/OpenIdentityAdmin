using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Bootstrap.Initialize;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Shares persisted synthetic enrollment setup without bypassing native password/bootstrap creation.</summary>
internal sealed class FactorScenario : IAsyncDisposable
{
    private const string MissingUserMessage = "Synthetic bootstrap user missing.";
    private const string IdentifierFormat = "N";

    private const string EnrollmentPurpose = "Enrollment";
    private const string PendingState = "Pending";
    private const int LifetimeSeconds = 300;
    private readonly AsyncServiceScope scope;
    internal IServiceProvider Services => scope.ServiceProvider;
    internal PlatformDbContext Database
    {
        get;
    }
    internal UserManager<PlatformUser> Users
    {
        get;
    }
    internal PlatformUser User { get; private set; } = null!;
    internal FactorAccess Access
    {
        get;
    }
    internal FactorProtection Protection
    {
        get;
    }
    internal AdministratorEnrollment Enrollment { get; private set; } = null!;
    internal CancellationToken Token { get; } = TestContext.Current.CancellationToken;
    internal const string Seed = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    /// <summary>Creates one scoped real manager/store/context graph.</summary>
    internal FactorScenario(FactorServices services)
    {
        scope = services.Services.CreateAsyncScope();
        Database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        Access = scope.ServiceProvider.GetRequiredService<FactorAccess>();
        Protection = scope.ServiceProvider.GetRequiredService<FactorProtection>();
    }

    /// <summary>Creates the same disabled Pending administrator as the real protected bootstrap command.</summary>
    internal async Task InitializeAsync(BootstrapDatabase lease)
    {
        Assert.Equal(BootstrapOutcome.Created, await lease.InitializeAsync(Token));
        User = await Users.FindByNameAsync(lease.Username) ?? throw new InvalidOperationException(MissingUserMessage);
        Enrollment = await Database.Set<AdministratorEnrollment>().SingleAsync(Token);
        _ = await Database.Set<AdministratorAuthentication>().SingleAsync(Token);
    }

    /// <summary>Persists a synthetic interaction; its existence grants no endpoint or session authority.</summary>
    internal async Task<AdministratorInteraction> StageAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var identifier = Guid.NewGuid().ToString(IdentifierFormat);
        Database.Add(new AdministratorBrowserBudget { Id = identifier, ExpiresAt = now + LifetimeSeconds });
        var interaction = new AdministratorInteraction
        {
            UserId = User.Id,
            PublicId = identifier,
            BrowserBudgetId = identifier,
            BrowserDigest = identifier,
            Purpose = EnrollmentPurpose,
            State = PendingState,
            SecurityStamp = User.SecurityStamp!,
            CreatedAt = now,
            ExpiresAt = now + LifetimeSeconds,
            PasswordVerifiedAt = now,
            StagedSeed = Protection.Protect(Seed, User.Id, 1, FactorProtection.Staged, identifier)
        };
        Database.Add(interaction);
        await Database.SaveChangesAsync(Token);
        return interaction;
    }

    /// <summary>Persists an inert recovery generation for store-level tests without changing enrollment or user authority.</summary>
    internal async Task SelectRecoveryGenerationAsync(long generation = 1)
    {
        Enrollment.RecoveryGeneration = generation;
        await Database.SaveChangesAsync(Token);
    }

    /// <summary>Disposes the shared scoped context and native manager.</summary>
    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
