using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Creates genuine additional session authority before opening a proof transaction; it never fabricates a session capability.</summary>
internal static class ProofSessions
{
    private const string Login = "Login";
    private const string DistinctSessions = "Native login must issue a distinct full-session identifier.";

    /// <summary>Obtains another full session from native password/TOTP login, proving the later substitution identifies real distinct authority.</summary>
    internal static async Task<string> OtherAsync(GovernanceScenario scenario)
    {
        var code = await scenario.UnusedCodeAsync();
        await using var scope = scenario.CreateScope();
        var services = scope.ServiceProvider;
        var token = TestContext.Current.CancellationToken;
        var correlation = CeremonySecrets.Create();
        var pending = await services.GetRequiredService<JourneyPassword>().BeginAsync(scenario.Journey.Lease.Username,
            scenario.Password, Login, CeremonySecrets.Create(), correlation, token);
        Assert.NotNull(pending.Interaction);
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var grant = await services.GetRequiredService<JourneyCompletion>().CompleteAsync(pending.Interaction, correlation, Login, code, token);
        FactorOracle.RequireWindow(scenario.Journey.Seed, code, started, true);
        Assert.NotNull(grant);
        Assert.Equal(scenario.ActorId, grant.UserId);
        Assert.True(scenario.Grant.SessionId != grant.SessionId, DistinctSessions);
        return grant.SessionId;
    }

    /// <summary>Rebinds only the tracked original session row to a distinct real session identifier; no impossible duplicate identifier is persisted.</summary>
    internal static async Task RebindTrackedAsync(ProofOwner owner, GovernanceScenario scenario, string other)
    {
        var database = owner.Operation.Get<PlatformDbContext>();
        var token = owner.Operation.Transaction.Token;
        var alternative = await database.Set<AdministratorSession>().SingleAsync(row => row.PublicId == other, token);
        var original = await database.Set<AdministratorSession>().SingleAsync(row => row.PublicId == scenario.Grant.SessionId, token);
        Assert.NotEqual(original.Id, alternative.Id);
        Assert.Equal(original.UserId, alternative.UserId);
        original.PublicId = alternative.PublicId;
    }
}
