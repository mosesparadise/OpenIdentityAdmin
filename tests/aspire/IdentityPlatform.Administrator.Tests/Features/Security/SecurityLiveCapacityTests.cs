using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Security;

/// <summary>Checks live-session replacement capacity separately from the retained-storage hard limit.</summary>
[Collection("administrator-bootstrap")]
public sealed class SecurityLiveCapacityTests(BootstrapFixture fixture)
{
    private const int LiveLimit = 8;

    /// <summary>Regeneration may replace current authority at the live limit without allocating over the retained cap.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public Task Given_EightLiveSessions_When_RecoveryCodesAreRegenerated_Then_PredecessorsRemainRevokedAndOneSessionIsCurrent(string provider) =>
        ReplaceAtLimitAsync(provider, false);

    /// <summary>Authenticator replacement has the same live-session exemption only for the validated initiating session.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public Task Given_EightLiveSessions_When_AuthenticatorIsReplaced_Then_PredecessorsRemainRevokedAndOneSessionIsCurrent(string provider) =>
        ReplaceAtLimitAsync(provider, true);

    /// <summary>Uses real enrollment and native proof while independently seeding only capacity occupancy.</summary>
    private async Task ReplaceAtLimitAsync(string provider, bool replacement)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        await SecurityCapacity.FillSessionsAsync(scenario, LiveLimit, false);
        var previous = await scenario.Database.Set<AdministratorSession>().AsNoTracking().Select(row => row.PublicId).ToArrayAsync(token);
        var login = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Login);
        var code = await SecurityScenario.UnusedAsync(scenario);
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(login, scenario.Correlation, SecurityScenario.Login, code, token));
        var purpose = replacement ? SecurityScenario.Replacement : SecurityScenario.Regeneration;
        var interaction = await SecurityScenario.BeginAsync(scenario, purpose, grant);
        var correlation = scenario.Correlation;
        if (replacement)
        {
            correlation = CeremonySecrets.Create();
            interaction = (await scenario.Get<JourneyTransition>().AuthorizeAsync(interaction, scenario.Correlation, purpose, code,
                correlation, token, SecurityScenario.Candidate(grant)))!;
            Assert.NotNull(interaction);
            var seed = await scenario.Get<JourneyStaging>().ReadAsync(interaction, correlation, token, purpose, SecurityScenario.Candidate(grant));
            Assert.NotNull(seed);
            code = FactorOracle.Code(seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
        var completed = await scenario.Get<JourneyCompletion>().CompleteAsync(interaction, correlation, purpose, code, token, SecurityScenario.Candidate(grant));
        Assert.NotNull(completed);
        var sessions = await scenario.Database.Set<AdministratorSession>().AsNoTracking().ToArrayAsync(token);
        Assert.Equal(LiveLimit + 1, sessions.Length);
        Assert.Equal(previous.Order(), sessions.Where(row => row.Revoked).Select(row => row.PublicId).Order());
        Assert.Equal(completed.SessionId, Assert.Single(sessions, row => !row.Revoked).PublicId);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(completed.UserId, completed.SessionId, token));
    }
}
