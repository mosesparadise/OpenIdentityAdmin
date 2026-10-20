using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Reuses the actual protected bootstrap and native enrollment to supply one committed actor session.</summary>
internal sealed class GovernanceScenario : IAsyncDisposable
{
    private const int StepSeconds = 30;
    private const int NativeWindowSteps = 2;
    private const string NoUnusedCode = "No unused independent native-window code was available.";
    private readonly JourneyScenario journey;
    internal JourneyCompletion.Grant Grant
    {
        get;
    }
    internal JourneyScenario Journey => journey;
    internal string ActorId => Grant.UserId;
    internal JourneySessionCandidate Session => new(Grant.UserId, Grant.SessionId);
    internal string Password => journey.Lease.Password;

    /// <summary>Retains the actual completion result; no private production proof is constructed by the fixture.</summary>
    private GovernanceScenario(JourneyScenario journey, JourneyCompletion.Grant grant)
    {
        this.journey = journey;
        Grant = grant;
    }

    /// <summary>Composes the existing journey fixture and returns only after native enrollment has committed.</summary>
    internal static async Task<GovernanceScenario> CreateAsync(BootstrapFixture fixture, string provider,
        Action<IServiceCollection>? configure = null, Uri? issuer = null)
    {
        var journey = await JourneyScenario.CreateAsync(fixture, provider, services =>
        {
            services.AddAdministratorGovernance();
            configure?.Invoke(services);
        }, issuer);
        try
        {
            return new GovernanceScenario(journey, await journey.EnrollAsync());
        }
        catch
        {
            await journey.DisposeAsync();
            throw;
        }
    }

    /// <summary>Creates a distinct scoped native context without duplicating the existing issuer and lease owners.</summary>
    internal AsyncServiceScope CreateScope() => journey.CreateScope();

    /// <summary>Runs one actual internal command in a fresh scope using independently generated native-window input.</summary>
    internal async Task<GovernanceOutcome> ExecuteAsync(GovernanceRequest request, string? password = null, string? code = null,
        JourneySessionCandidate? session = null)
    {
        await using var scope = CreateScope();
        var submitted = code ?? await UnusedCodeAsync();
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var result = await scope.ServiceProvider.GetRequiredService<GovernanceService>().ExecuteAsync(request,
            session ?? Session, password ?? Password, submitted, TestContext.Current.CancellationToken);
        if (code is null)
        {
            FactorOracle.RequireWindow(journey.Seed, submitted, started, true);
        }
        return result;
    }

    /// <summary>Obtains a detached observation through the production serialized read operation in a fresh scope.</summary>
    internal async Task<GovernanceObservation> ReadAsync(string? actorId = null)
    {
        await using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GovernanceService>().ReadAsync(actorId ?? ActorId,
            TestContext.Current.CancellationToken);
    }

    /// <summary>Reads independent persisted target versions; malformed or stale requests remain explicit in individual tests.</summary>
    internal async Task<GovernanceRequest> RequestAsync(GovernanceAction action, string? userId = null,
        string? roleId = null, string? permission = null)
    {
        await using var scope = CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var governance = await database.Set<AdministratorGovernance>().AsNoTracking().SingleAsync(token);
        var user = userId is null ? null : await database.Users.AsNoTracking().SingleAsync(row => row.Id == userId, token);
        var enrollment = userId is null ? null : await database.Set<AdministratorEnrollment>().AsNoTracking()
            .SingleAsync(row => row.UserId == userId, token);
        var role = roleId is null ? null : await database.Roles.AsNoTracking().SingleAsync(row => row.Id == roleId, token);
        var versions = new GovernanceVersions(governance.AuthorityRevision, user?.ConcurrencyStamp,
            role?.ConcurrencyStamp, enrollment?.Revision);
        var request = GovernanceRequest.Create(action, versions, userId, roleId, permission);
        Assert.NotNull(request);
        return request;
    }

    /// <summary>Selects a native-window value unused by this exact actor and factor, including values accepted by other ceremonies.</summary>
    internal async Task<string> UnusedCodeAsync(string? userId = null, string? seed = null)
    {
        await using var scope = CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var owner = userId ?? ActorId;
        var material = seed ?? journey.Seed;
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking()
            .SingleAsync(row => row.UserId == owner, token);
        var replay = await database.Set<AdministratorReplay>().AsNoTracking()
            .Where(row => row.UserId == owner && row.Generation == enrollment.FactorGeneration).ToArrayAsync(token);
        var protection = scope.ServiceProvider.GetRequiredService<FactorProtection>();
        var accepted = replay.Select(row => protection.Unprotect(row.ProtectedCode, row.UserId, row.Generation,
            FactorProtection.Replay)).ToHashSet(StringComparer.Ordinal);
        int[] offsets = [0, StepSeconds, -StepSeconds, StepSeconds * NativeWindowSteps, -StepSeconds * NativeWindowSteps];
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var code = offsets.Select(offset => FactorOracle.Code(material, now + offset))
            .FirstOrDefault(value => !accepted.Contains(value));
        Assert.True(code is not null, NoUnusedCode);
        return code!;
    }

    /// <summary>Reads a detached, nonsecret rollback witness after all competing operations have been joined.</summary>
    internal async Task<GovernanceSnapshot> SnapshotAsync()
    {
        await using var scope = CreateScope();
        return await GovernanceSnapshot.ReadAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
    }

    /// <summary>Delegates complete ownership and cleanup to the existing journey fixture.</summary>
    public ValueTask DisposeAsync() => journey.DisposeAsync();
}
