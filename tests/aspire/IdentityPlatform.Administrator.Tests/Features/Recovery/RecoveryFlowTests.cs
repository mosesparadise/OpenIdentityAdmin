using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Recovery;

/// <summary>Checks native restricted recovery, same-slot continuity and exact-destination completion without invented factor state.</summary>
[Collection("administrator-bootstrap")]
public sealed class RecoveryFlowTests(BootstrapFixture fixture)
{
    private const string ReusedRecoveryMaterial = "New recovery material reused a previously disclosed code.";
    /// <summary>One actual native code claim revokes old authority but neither creates a session nor deletes the remaining codes.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ActiveAccount_When_OneRecoveryCodeIsRedeemed_Then_OnlySameSlotReplacementAuthorityCommits(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var before = await SecurityScenario.InteractionAsync(scenario, initial);
        var verifiers = await SecuritySnapshot.RecoveryAsync(scenario.Database);
        var correlation = CeremonySecrets.Create();
        var replacement = await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], correlation, token);
        Assert.NotNull(replacement);
        var after = await SecurityScenario.InteractionAsync(scenario, replacement);
        SecuritySnapshot.RequireSingleClaim(verifiers, await SecuritySnapshot.RecoveryAsync(scenario.Database), grant.RecoveryCodes[0]);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.BrowserBudgetId, after.BrowserBudgetId);
        Assert.Equal(before.PasswordVerifiedAt, after.PasswordVerifiedAt);
        Assert.Equal(before.ExpiresAt, after.ExpiresAt);
        Assert.True(before.PublicId != after.PublicId && before.BrowserDigest != after.BrowserDigest);
        Assert.Equal(1, await scenario.Database.Set<AdministratorSession>().CountAsync(token));
        Assert.Null(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
        Assert.Equal(9, await scenario.Database.Set<AdministratorRecoveryCode>().CountAsync(row => row.RedeemedAt == null, token));
        Assert.Equal(1, await scenario.Database.Set<AdministratorRecoveryCode>().CountAsync(row => row.RedeemedAt != null, token));
        Assert.Equal("RecoveryRequired", (await scenario.Database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(token)).State);
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(initial, scenario.Correlation, token, SecurityScenario.Replacement));
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(replacement, correlation, SecurityScenario.Recovery,
            FactorOracle.Code(scenario.Seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), token));
        Assert.Null((await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password, SecurityScenario.Login,
            scenario.Browser, scenario.Correlation, token)).Interaction);
        Assert.NotNull(await scenario.Get<JourneyStaging>().ReadAsync(replacement, correlation, token, SecurityScenario.Replacement));
    }

    /// <summary>Only the proven newly allocated seed becomes active, with a retained replay reservation and fresh one-time codes.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RestrictedRecovery_When_NewAuthenticatorIsConfirmed_Then_ExactDestinationAndNewCodesCommit(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var original = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var correlation = CeremonySecrets.Create();
        var replacement = await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
            original.RecoveryCodes[0], correlation, token);
        Assert.NotNull(replacement);
        var row = await SecurityScenario.InteractionAsync(scenario, replacement);
        var seed = await scenario.Get<JourneyStaging>().ReadAsync(replacement, correlation, token, SecurityScenario.Replacement);
        Assert.NotNull(seed);
        var code = FactorOracle.Code(seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var grant = await scenario.Get<JourneyCompletion>().CompleteAsync(replacement, correlation, SecurityScenario.Replacement, code, token);
        Assert.NotNull(grant);
        Assert.Equal(10, grant.RecoveryCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.False(grant.RecoveryCodes.Intersect(original.RecoveryCodes, StringComparer.Ordinal).Any(), ReusedRecoveryMaterial);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
        Assert.Null(await scenario.Get<JourneySessions>().ValidateAsync(original.UserId, original.SessionId, token));
        var enrollment = await scenario.Database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(token);
        Assert.Equal("Active", enrollment.State);
        Assert.Equal(row.DestinationGeneration, enrollment.FactorGeneration);
        Assert.Single(await scenario.Database.Set<AdministratorFactor>().AsNoTracking().ToArrayAsync(token));
        Assert.Equal(1, await scenario.Database.Set<AdministratorReplay>().CountAsync(value => value.Generation == row.DestinationGeneration, token));
        Assert.Equal(10, await scenario.Database.Set<AdministratorRecoveryCode>().CountAsync(token));
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(replacement, correlation, SecurityScenario.Replacement, code, token));
    }

    /// <summary>Losing staging never turns the password into recovery authority; another unused code is mandatory.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_LostRecoveryStage_When_FreshPasswordIsUsed_Then_SpentCodeDeniesAndAnotherCodeCanResume(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var lost = await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], CeremonySecrets.Create(), token);
        Assert.NotNull(lost);
        var next = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(next, scenario.Correlation, token, SecurityScenario.Replacement));
        Assert.Null(await scenario.Get<JourneyTransition>().AuthorizeAsync(next, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], CeremonySecrets.Create(), token));
        Assert.NotNull(await scenario.Get<JourneyTransition>().AuthorizeAsync(next, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[1], CeremonySecrets.Create(), token));
        Assert.Equal(8, await scenario.Database.Set<AdministratorRecoveryCode>().CountAsync(row => row.RedeemedAt == null, token));
        Assert.Equal(1, await scenario.Database.Set<AdministratorSession>().CountAsync(token));
        Assert.Equal("Consumed", (await SecurityScenario.InteractionAsync(scenario, lost)).State);
    }
}
