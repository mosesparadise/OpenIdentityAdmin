using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Runs internal governance against the exact real HTTP environment's database, issuer and protection ring without owning another graph.</summary>
internal sealed class GovernanceHttpBridge : IAsyncDisposable
{
    private const int StepSeconds = 30;
    private const int WindowSteps = 2;
    private const string MissingCode = "No unused independent native-window code was available.";
    private readonly FactorServices services;
    internal GovernanceHttpActor Original
    {
        get;
    }

    /// <summary>Owns only the extra native service provider; the caller retains graph, lease, material and HTTP client ownership.</summary>
    private GovernanceHttpBridge(FactorServices services, GovernanceHttpActor original)
    {
        this.services = services;
        Original = original;
    }

    /// <summary>Attaches to the sole genuinely issued initial HTTP session after enrollment; no native or browser authentication is manufactured.</summary>
    internal static async Task<GovernanceHttpBridge> CreateAsync(AdministratorEnvironment environment, string provider, SecurityEnrollment enrollment)
    {
        var services = new FactorServices(provider, environment.Database, environment.Material, TimeProvider.System,
            collection => collection.AddAdministratorJourneys().AddAdministratorGovernance()
                .AddScoped<JourneyPassword>().AddScoped<JourneyCompletion>().AddScoped<JourneyActivation>());
        try
        {
            await using var scope = services.Services.CreateAsyncScope();
            var session = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AdministratorSession>()
                .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.False(session.Revoked);
            var actor = new GovernanceHttpActor(new JourneySessionCandidate(session.UserId, session.PublicId), environment.Database.Password, enrollment.Seed);
            return new GovernanceHttpBridge(services, actor);
        }
        catch
        {
            await services.DisposeAsync();
            throw;
        }
    }

    /// <summary>Creates independent native scoped dependencies using the exact environment runtime-role connection.</summary>
    internal AsyncServiceScope CreateScope() => services.Services.CreateAsyncScope();

    /// <summary>Provisions an additional usable platform witness explicitly as fixture data and obtains its session through native password/TOTP.</summary>
    internal async Task<GovernanceHttpActor> WitnessAsync()
    {
        var password = GovernanceAccounts.Password(Original.Password);
        var user = await GovernanceAccounts.AddAsync(CreateScope, password);
        return new GovernanceHttpActor(await GovernanceAccounts.LoginAsync(CreateScope, password, user), password, FactorScenario.Seed);
    }

    /// <summary>Reads explicit current native versions before preparing immutable intent for actual governed execution.</summary>
    internal async Task<GovernanceRequest> RequestAsync(GovernanceAction action, string? user = null, string? role = null, string? permission = null)
    {
        await using var scope = CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var governance = await database.Set<AdministratorGovernance>().AsNoTracking().SingleAsync(token);
        var target = user is null ? null : await database.Users.AsNoTracking().SingleAsync(row => row.Id == user, token);
        var enrollment = user is null ? null : await database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(row => row.UserId == user, token);
        var targetRole = role is null ? null : await database.Roles.AsNoTracking().SingleAsync(row => row.Id == role, token);
        var request = GovernanceRequest.Create(action, new GovernanceVersions(governance.AuthorityRevision, target?.ConcurrencyStamp,
            targetRole?.ConcurrencyStamp, enrollment?.Revision), user, role, permission);
        Assert.NotNull(request);
        return request;
    }

    /// <summary>Executes genuine command proof with an independent unused native input and never catches or retries an unavailable result.</summary>
    internal async Task<GovernanceOutcome> ExecuteAsync(GovernanceHttpActor actor, GovernanceRequest request)
    {
        var code = await UnusedAsync(actor);
        await using var scope = CreateScope();
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            return await scope.ServiceProvider.GetRequiredService<GovernanceService>().ExecuteAsync(request, actor.Session,
                actor.Password, code, TestContext.Current.CancellationToken);
        }
        finally { FactorOracle.RequireWindow(actor.Seed, code, started, true); }
    }

    /// <summary>Selects a valid value not present in the actual shared protected replay ledger; it does not assume earlier HTTP codes are unspent.</summary>
    internal async Task<string> UnusedAsync(GovernanceHttpActor actor)
    {
        await using var scope = CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var generation = await database.Set<AdministratorEnrollment>().Where(row => row.UserId == actor.UserId).Select(row => row.FactorGeneration).SingleAsync(token);
        var replay = await database.Set<AdministratorReplay>().AsNoTracking().Where(row => row.UserId == actor.UserId && row.Generation == generation).ToArrayAsync(token);
        var protection = scope.ServiceProvider.GetRequiredService<FactorProtection>();
        var used = replay.Select(row => protection.Unprotect(row.ProtectedCode, actor.UserId, generation, FactorProtection.Replay)).ToHashSet(StringComparer.Ordinal);
        int[] offsets = [0, StepSeconds, -StepSeconds, StepSeconds * WindowSteps, -StepSeconds * WindowSteps];
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var value = offsets.Select(offset => FactorOracle.Code(actor.Seed, now + offset)).FirstOrDefault(code => !used.Contains(code));
        Assert.True(value is not null, MissingCode);
        return value!;
    }

    /// <summary>Reads current permissions/population through actual serialized governance policy.</summary>
    internal async Task<GovernanceObservation> ObserveAsync()
    {
        await using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GovernanceService>().ReadAsync(Original.UserId, TestContext.Current.CancellationToken);
    }

    /// <summary>Reads complete detached security and authority digests after native/HTTP writers have completed.</summary>
    internal async Task<GovernanceSnapshot> SnapshotAsync()
    {
        await using var scope = CreateScope();
        return await GovernanceSnapshot.ReadAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
    }

    /// <summary>Disposes the additional native provider while leaving actual graph/material/database ownership to the caller.</summary>
    public ValueTask DisposeAsync() => services.DisposeAsync();
}
