using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Advances repeated expiry cycles without registering a cleaner, distinguishing physical storage ceilings from live authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class RetainedExpiryTests(BootstrapFixture fixture)
{
    private const int ExpiryCycles = 2;
    private const int ExpirySeconds = 301;
    private const int RetainedRows = 10000;
    private const int SessionRows = 32;

    /// <summary>Expired retained interactions still deny new allocation through repeated cycles while a genuine session remains usable.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_NoCleanerAndFullInteractions_When_ExpiryRepeats_Then_NewAdmissionCannotEvictRowsOrInvalidateExistingSession(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Login);
        await SecurityCapacity.FillInteractionsAsync(scenario, initial);
        for (var cycle = 0; cycle < ExpiryCycles; cycle++)
        {
            scenario.Clock.Now = scenario.Clock.Now.AddSeconds(ExpirySeconds);
            var result = await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password, SecurityScenario.Login,
                CeremonySecrets.Create(), CeremonySecrets.Create(), TestContext.Current.CancellationToken);
            Assert.Null(result.Interaction);
            Assert.Equal(RetainedRows, await scenario.Database.Set<AdministratorInteraction>().CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, await scenario.Database.Set<AdministratorBrowserBudget>().CountAsync(TestContext.Current.CancellationToken));
            Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
        }
    }

    /// <summary>Existing browser budgets remain usable at the new-budget ceiling; each fresh voluntary step-up reuses its admitted slot.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_NoCleanerAndFullBrowserStorage_When_ExpiryRepeats_Then_NewBrowsersDenyWhileKnownBrowserAndNonallocatingActionsWork(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        await FillBrowsersAsync(scenario);
        for (var cycle = 0; cycle < ExpiryCycles; cycle++)
        {
            scenario.Clock.Now = scenario.Clock.Now.AddSeconds(ExpirySeconds);
            var token = TestContext.Current.CancellationToken;
            var denied = await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password, SecurityScenario.Login,
                CeremonySecrets.Create(), CeremonySecrets.Create(), token);
            Assert.Null(denied.Interaction);
            var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Replacement, grant);
            var original = await SecurityScenario.InteractionAsync(scenario, initial);
            var replacement = await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Replacement,
                await SecurityScenario.UnusedAsync(scenario), CeremonySecrets.Create(), token, SecurityScenario.Candidate(grant));
            Assert.NotNull(replacement);
            Assert.Equal(original.Id, (await SecurityScenario.InteractionAsync(scenario, replacement)).Id);
            Assert.Equal(RetainedRows, await scenario.Database.Set<AdministratorBrowserBudget>().CountAsync(token));
            Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
        }
    }

    /// <summary>Expired sessions still occupy retained capacity; denied native completion cannot reserve even a valid unused proof.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_NoCleanerAndFullSessionStorage_When_ExpiryRepeats_Then_CompletionConsumesNoProofAndOriginalLiveSessionSurvives(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        await SecurityCapacity.FillSessionsAsync(scenario);
        var token = TestContext.Current.CancellationToken;
        var expiry = scenario.Clock.Now.ToUnixTimeSeconds() + ExpirySeconds - 1;
        await scenario.Database.Set<AdministratorSession>().Where(row => row.PublicId != grant.SessionId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revoked, false).SetProperty(row => row.ExpiresAt, expiry), token);
        for (var cycle = 0; cycle < ExpiryCycles; cycle++)
        {
            scenario.Clock.Now = scenario.Clock.Now.AddSeconds(ExpirySeconds);
            var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Login);
            var before = await SecuritySnapshot.ReadAsync(scenario.Database);
            Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(initial, scenario.Correlation, SecurityScenario.Login,
                await SecurityScenario.UnusedAsync(scenario), token));
            Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
            Assert.Equal(SessionRows, await scenario.Database.Set<AdministratorSession>().CountAsync(token));
            Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
        }
    }

    /// <summary>Seeds only inert unreferenced expired budget occupancy, preserving the real browser's admitted row.</summary>
    private static async Task FillBrowsersAsync(JourneyScenario scenario)
    {
        var token = TestContext.Current.CancellationToken;
        var count = await scenario.Database.Set<AdministratorBrowserBudget>().CountAsync(token);
        for (var index = count; index < RetainedRows; index++)
        {
            scenario.Database.Add(new AdministratorBrowserBudget { Id = CeremonySecrets.Create(), ExpiresAt = 1 });
        }
        await scenario.Database.SaveChangesAsync(token);
    }
}
