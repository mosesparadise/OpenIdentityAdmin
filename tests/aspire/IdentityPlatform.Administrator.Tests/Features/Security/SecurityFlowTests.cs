using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Security;

/// <summary>Checks actual session-bound native step-up, safe abandonment, verifier regeneration and exact-session logout.</summary>
[Collection("administrator-bootstrap")]
public sealed class SecurityFlowTests(BootstrapFixture fixture)
{
    private const string LogoutMarker = "administrator-logout-revocation-bypass";
    private const string LogoutEvent = "administrator-logout";
    private const string ReusedRecoveryMaterial = "New recovery material reused a previously disclosed code.";
    /// <summary>Password and old TOTP authorize staging only for the initiating session; abandoning it leaves the old factor intact.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CurrentSession_When_VoluntaryReplacementIsStaged_Then_OldAuthorityRemainsAndForeignSessionCannotComplete(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var login = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Login);
        var foreign = await scenario.Get<JourneyCompletion>().CompleteAsync(login, scenario.Correlation, SecurityScenario.Login,
            await SecurityScenario.UnusedAsync(scenario), token);
        Assert.NotNull(foreign);
        var candidate = SecurityScenario.Candidate(grant);
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Replacement, grant);
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(initial, scenario.Correlation, token, SecurityScenario.Replacement, candidate));
        var code = await SecurityScenario.UnusedAsync(scenario);
        var correlation = CeremonySecrets.Create();
        var replacement = await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Replacement,
            code, correlation, token, candidate);
        Assert.NotNull(replacement);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(replacement, correlation, token, SecurityScenario.Replacement));
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(replacement, correlation, token, SecurityScenario.Replacement, SecurityScenario.Candidate(foreign)));
        var seed = await scenario.Get<JourneyStaging>().ReadAsync(replacement, correlation, token, SecurityScenario.Replacement, candidate);
        Assert.NotNull(seed);
        var confirmation = FactorOracle.Code(seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(replacement, correlation, SecurityScenario.Replacement, confirmation, token));
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(replacement, correlation, SecurityScenario.Replacement, confirmation, token, SecurityScenario.Candidate(foreign)));
        var completed = await scenario.Get<JourneyCompletion>().CompleteAsync(replacement, correlation, SecurityScenario.Replacement, confirmation, token, candidate);
        Assert.NotNull(completed);
        Assert.Null(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(completed.UserId, completed.SessionId, token));
    }

    /// <summary>Regeneration reserves the current factor code, changes all recovery verifiers and replaces the initiating session after commit.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CurrentSession_When_RecoveryCodesAreRegenerated_Then_OldCodesAndSessionsDenyWhileFactorReplayRemains(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Regeneration, grant);
        var code = await SecurityScenario.UnusedAsync(scenario);
        var completed = await scenario.Get<JourneyCompletion>().CompleteAsync(initial, scenario.Correlation, SecurityScenario.Regeneration,
            code, token, SecurityScenario.Candidate(grant));
        Assert.NotNull(completed);
        Assert.Equal(10, completed.RecoveryCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.False(completed.RecoveryCodes.Intersect(grant.RecoveryCodes, StringComparer.Ordinal).Any(), ReusedRecoveryMaterial);
        Assert.Equal(2, await scenario.Database.Set<AdministratorReplay>().CountAsync(token));
        Assert.Equal(1, (await scenario.Database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(token)).FactorGeneration);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(completed.UserId, completed.SessionId, token));
        Assert.Null(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(initial, scenario.Correlation, SecurityScenario.Regeneration,
            code, token, SecurityScenario.Candidate(completed)));
        var recovery = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        Assert.Null(await scenario.Get<JourneyTransition>().AuthorizeAsync(recovery, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], CeremonySecrets.Create(), token));
    }

    /// <summary>Logging out one actual native session revokes only that session and preserves another valid session.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    [Trait("CriticalDecision", "administrator-logout-revocation")]
    public async Task Given_TwoCurrentSessions_When_OneSessionLogsOut_Then_OnlyItsStoredAuthorityIsRevoked(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var first = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Login);
        var second = await scenario.Get<JourneyCompletion>().CompleteAsync(initial, scenario.Correlation, SecurityScenario.Login,
            await SecurityScenario.UnusedAsync(scenario), token);
        Assert.NotNull(second);
        Assert.True(await scenario.Get<JourneyLogout>().RevokeAsync(SecurityScenario.Candidate(first), token));
        Assert.True(await scenario.Get<JourneySessions>().ValidateAsync(first.UserId, first.SessionId, token) is null, LogoutMarker);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(second.UserId, second.SessionId, token));
        Assert.False(await scenario.Get<JourneyLogout>().RevokeAsync(SecurityScenario.Candidate(first), token));
        Assert.Equal(1, await scenario.Database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == LogoutEvent, token));
    }
}
