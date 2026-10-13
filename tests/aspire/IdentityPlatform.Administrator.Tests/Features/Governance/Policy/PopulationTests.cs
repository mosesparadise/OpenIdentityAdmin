using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Policy;

/// <summary>Checks configured survivor observations without accepting session cookies as population proof.</summary>
[Collection("administrator-bootstrap")]
public sealed class PopulationTests(BootstrapFixture fixture)
{
    /// <summary>Repeated current reads roll back coordination locks and preserve every persisted security and authority field.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CurrentPopulation_When_EvaluatedRepeatedly_Then_SecurityStateAndSemanticVersionRemainUnchanged(string provider)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var before = await scenario.SnapshotAsync();
        await using (var scope = scenario.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<GovernanceService>();
            var first = await service.ReadAsync(scenario.ActorId, TestContext.Current.CancellationToken);
            var second = await service.ReadAsync(scenario.ActorId, TestContext.Current.CancellationToken);
            Assert.Equal(0, first.AuthorityRevision);
            Assert.Equal(first.AuthorityRevision, second.AuthorityRevision);
            Assert.Equal(25, first.Permissions.Count);
            Assert.Equal(first.Permissions.Order(StringComparer.Ordinal), second.Permissions.Order(StringComparer.Ordinal));
            Assert.Equal(scenario.ActorId, Assert.Single(first.UsableAdministrators));
            Assert.Equal(first.UsableAdministrators, second.UsableAdministrators);
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Enumerates the thirty approved missing-condition rows on both actual production providers.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> MissingConditions => GovernanceRows.For(PopulationCases.MissingConditions);

    /// <summary>Distinguishes provider constraint rejection, typed unavailable reads and successful exclusion without changing durable state.</summary>
    [Theory, MemberData(nameof(MissingConditions))]
    public async Task Given_OtherwiseUsableAdministrator_When_OneRequiredConditionIsMissing_Then_AccountIsExcluded(string provider, string condition)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        await PopulationEvidence.RequireCompleteAsync(scenario);
        if (PopulationCases.RequiresConstraint(condition))
        {
            await PopulationConstraints.RequireRejectedAsync(scenario, provider, condition);
            return;
        }
        await PopulationDamage.ApplyAsync(scenario, condition);
        var before = await scenario.SnapshotAsync();
        if (PopulationCases.RequiresUnavailable(condition))
        {
            await Assert.ThrowsAsync<GovernanceUnavailableException>(() => scenario.ReadAsync());
        }
        else
        {
            var result = await scenario.ReadAsync();
            Assert.DoesNotContain(scenario.ActorId, (IReadOnlySet<string>)result.UsableAdministrators);
            Assert.Empty(result.UsableAdministrators);
            if (condition == PopulationCases.MissingPlatformGrantWithOverlap)
            {
                PopulationEvidence.RequireCompleteGrants(result.Permissions);
            }
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Enumerates each unusable active-material form beneath otherwise valid staging on both providers.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> StagedRows =>
        GovernanceRows.For(PopulationCases.MissingActive, PopulationCases.CorruptActive);

    /// <summary>Enumerates the three real ambient selection leases; no setters, reflection or private-constructor bypass is used.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> SelectionRows =>
        GovernanceRows.For(PopulationCases.Staged, PopulationCases.Recovery, PopulationCases.Installation);

    /// <summary>Proves native-valid staged material cannot compensate for missing or corrupt active credentials, without persistent read side effects.</summary>
    [Theory, MemberData(nameof(StagedRows))]
    public async Task Given_ValidStagedFactor_When_ActiveMaterialIsUnusable_Then_StagedAccessCannotSupplySurvivorAuthority(string provider, string condition)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        await PopulationEvidence.RequireCompleteAsync(scenario);
        var interaction = await PopulationSelection.StageAsync(scenario);
        var damage = condition == PopulationCases.MissingActive ? PopulationCases.MissingActiveFactor : PopulationCases.CorruptActiveFactor;
        Assert.True(await PopulationMaterial.TryApplyAsync(scenario, damage));
        await Assert.ThrowsAsync<GovernanceUnavailableException>(() => scenario.ReadAsync());
        var before = await scenario.SnapshotAsync();
        await RequireSelectionDenialAsync(scenario, interaction, PopulationCases.Staged, activeUsable: false);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Requires active-only governance to reject every real ambient material lease even when the account is otherwise usable.</summary>
    [Theory, MemberData(nameof(SelectionRows))]
    public async Task Given_ValidAmbientMaterialSelection_When_GovernanceEvaluatesActiveAuthority_Then_SelectionIsRejected(string provider, string selection)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        await PopulationEvidence.RequireCompleteAsync(scenario);
        var interaction = await PopulationSelection.StageAsync(scenario);
        var before = await scenario.SnapshotAsync();
        await RequireSelectionDenialAsync(scenario, interaction, selection, activeUsable: true);
        Assert.Equal(before, await scenario.SnapshotAsync());
        await PopulationEvidence.RequireCompleteAsync(scenario);
    }

    /// <summary>Evaluates the actual locked usability policy so a nested-transaction rejection cannot impersonate the ambient-selection guard.</summary>
    private static async Task RequireSelectionDenialAsync(GovernanceScenario scenario, Guid interaction, string selection, bool activeUsable)
    {
        await using var scope = scenario.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var discovery = services.GetRequiredService<GovernanceDiscovery>();
        var plan = await discovery.DiscoverAsync(scenario.ActorId, null, TestContext.Current.CancellationToken);
        await using var transaction = await CeremonyTransaction.OpenGovernanceAsync(database,
            services.GetRequiredService<NativeOperation>(), plan, TestContext.Current.CancellationToken);
        await discovery.RequireUnchangedAsync(transaction, scenario.ActorId, null);
        var grants = await services.GetRequiredService<GovernanceGrants>().ReadAsync(transaction, null);
        var account = await CeremonyAccount.LoadAsync(database, transaction, scenario.ActorId);
        var usability = services.GetRequiredService<GovernanceUsability>();
        using (await PopulationSelection.EnterAsync(services, scenario.ActorId, interaction, selection))
        {
            if (selection == PopulationCases.Staged)
            {
                await PopulationSelection.RequireValidStagingAsync(services, scenario.ActorId);
            }
            await Assert.ThrowsAsync<FactorUnavailableException>(() => usability.IsUsableAsync(transaction, account, grants));
        }
        if (activeUsable)
        {
            Assert.True(await usability.IsUsableAsync(transaction, account, grants));
        }
        else
        {
            await Assert.ThrowsAsync<FactorUnavailableException>(() => usability.IsUsableAsync(transaction, account, grants));
        }
    }

    /// <summary>Enumerates native logout and genuine product-clock expiry for both actual database providers.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> SessionRows =>
        GovernanceRows.For(PopulationCases.LoggedOut, PopulationCases.SessionExpired);

    /// <summary>Proves absent current session authority and absent recovery codes do not destroy a configured usable administrator.</summary>
    [Theory, MemberData(nameof(SessionRows))]
    public async Task Given_ConfiguredUsableAdministrator_When_NoCurrentSessionExists_Then_PopulationStillCountsTheAccount(string provider, string condition)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        await PopulationEvidence.RequireCompleteAsync(scenario);
        await RemoveCurrentSessionAsync(scenario, condition);
        await PopulationEvidence.RemoveRecoveryCodesAsync(scenario);
        var before = await scenario.SnapshotAsync();
        await using (var scope = scenario.CreateScope())
        {
            Assert.Null(await scope.ServiceProvider.GetRequiredService<JourneySessions>().ValidateAsync(
                scenario.ActorId, scenario.Grant.SessionId, TestContext.Current.CancellationToken));
        }
        await PopulationEvidence.RequireCompleteAsync(scenario);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Uses actual logout or advances only the test-owned product clock to the persisted absolute boundary; no invalid session row is fabricated.</summary>
    private static async Task RemoveCurrentSessionAsync(GovernanceScenario scenario, string condition)
    {
        await using var scope = scenario.CreateScope();
        var services = scope.ServiceProvider;
        var token = TestContext.Current.CancellationToken;
        if (condition == PopulationCases.LoggedOut)
        {
            Assert.True(await services.GetRequiredService<JourneyLogout>().RevokeAsync(scenario.Session, token));
        }
        else
        {
            Assert.Equal(PopulationCases.SessionExpired, condition);
            var database = services.GetRequiredService<PlatformDbContext>();
            var expiry = await database.Set<AdministratorSession>().Where(row => row.PublicId == scenario.Grant.SessionId)
                .Select(row => row.ExpiresAt).SingleAsync(token);
            scenario.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(expiry);
        }
    }
}
