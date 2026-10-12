using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Recovery;

/// <summary>Preserves recovery progress under physical interaction exhaustion without bypassing admission for new password attempts.</summary>
[Collection("administrator-bootstrap")]
public sealed class RecoveryCapacityTests(BootstrapFixture fixture)
{
    /// <summary>A native recovery claim transitions only its existing retained slot and cannot allocate an uncounted browser budget.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RetainedInteractionLimit_When_RecoveryTransitions_Then_ExistingSlotSucceedsAndNewAdmissionStillDenies(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var original = await SecurityScenario.InteractionAsync(scenario, initial);
        await SecurityCapacity.FillInteractionsAsync(scenario, initial);
        var correlation = CeremonySecrets.Create();
        var replacement = await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], correlation, token);
        Assert.NotNull(replacement);
        var row = await SecurityScenario.InteractionAsync(scenario, replacement);
        Assert.Equal(original.Id, row.Id);
        Assert.Equal(original.ExpiresAt, row.ExpiresAt);
        Assert.Equal(10000, await scenario.Database.Set<AdministratorInteraction>().CountAsync(token));
        Assert.Equal(1, await scenario.Database.Set<AdministratorBrowserBudget>().CountAsync(token));
        Assert.NotNull(await scenario.Get<JourneyStaging>().ReadAsync(replacement, correlation, token, SecurityScenario.Replacement));
        Assert.Null((await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password,
            SecurityScenario.Recovery, scenario.Browser, scenario.Correlation, token)).Interaction);
    }
}
