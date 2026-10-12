using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Security;

/// <summary>Requires allocation limits before native proof consumption while allowing authorized nonallocating slot transitions.</summary>
[Collection("administrator-bootstrap")]
public sealed class SecurityCapacityTests(BootstrapFixture fixture)
{
    /// <summary>Retained session exhaustion leaves the actual regeneration code and all old authority unchanged until legitimate space is available.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RetainedSessionLimit_When_RegenerationIsSubmitted_Then_ProofRemainsUsableAfterCapacityIsReleased(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Regeneration, grant);
        var code = await SecurityScenario.UnusedAsync(scenario);
        await SecurityCapacity.FillSessionsAsync(scenario);
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(initial, scenario.Correlation, SecurityScenario.Regeneration,
            code, token, SecurityScenario.Candidate(grant)));
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
        await scenario.Database.Set<AdministratorSession>().Where(row => row.Revoked).ExecuteDeleteAsync(token);
        Assert.NotNull(await scenario.Get<JourneyCompletion>().CompleteAsync(initial, scenario.Correlation, SecurityScenario.Regeneration,
            code, token, SecurityScenario.Candidate(grant)));
    }

    /// <summary>Replacement completion checks retained capacity before reserving the valid destination code or installing a factor.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RetainedSessionLimit_When_ReplacementIsConfirmed_Then_ExactUnspentDestinationCanCompleteLater(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var correlation = CeremonySecrets.Create();
        var replacement = await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], correlation, token);
        Assert.NotNull(replacement);
        var seed = await scenario.Get<JourneyStaging>().ReadAsync(replacement, correlation, token, SecurityScenario.Replacement);
        Assert.NotNull(seed);
        var code = FactorOracle.Code(seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await SecurityCapacity.FillSessionsAsync(scenario);
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(replacement, correlation, SecurityScenario.Replacement, code, token));
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
        await scenario.Database.Set<AdministratorSession>().Where(row => row.Revoked).ExecuteDeleteAsync(token);
        Assert.NotNull(await scenario.Get<JourneyCompletion>().CompleteAsync(replacement, correlation, SecurityScenario.Replacement, code, token));
    }

    /// <summary>A voluntary current-factor step-up rotates its existing slot even when physical interaction storage is full.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RetainedInteractionLimit_When_VoluntaryReplacementTransitions_Then_NoNewSlotOrBudgetIsAllocated(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Replacement, grant);
        var original = await SecurityScenario.InteractionAsync(scenario, initial);
        await SecurityCapacity.FillInteractionsAsync(scenario, initial);
        var replacement = await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Replacement,
            await SecurityScenario.UnusedAsync(scenario), CeremonySecrets.Create(), token, SecurityScenario.Candidate(grant));
        Assert.NotNull(replacement);
        var row = await SecurityScenario.InteractionAsync(scenario, replacement);
        Assert.Equal(original.Id, row.Id);
        Assert.Equal(original.ExpiresAt, row.ExpiresAt);
        Assert.Equal(10000, await scenario.Database.Set<AdministratorInteraction>().CountAsync(token));
        Assert.Equal(1, await scenario.Database.Set<AdministratorBrowserBudget>().CountAsync(token));
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
    }
}
