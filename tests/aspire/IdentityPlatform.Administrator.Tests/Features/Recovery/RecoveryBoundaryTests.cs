using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Recovery;

/// <summary>Rejects forged stage provenance, stale authority and exhausted recovery without introducing any reset bypass.</summary>
[Collection("administrator-bootstrap")]
public sealed class RecoveryBoundaryTests(BootstrapFixture fixture)
{
    /// <summary>Every changed native-authorization binding independently denies key disclosure and completion, with a restored positive control.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AuthorizedReplacement_When_ProofProvenanceChanges_Then_EachMismatchDeniesWithoutSpendingNewFactor(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var correlation = CeremonySecrets.Create();
        var replacement = await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], correlation, token);
        Assert.NotNull(replacement);
        var original = await SecurityScenario.InteractionAsync(scenario, replacement);
        var seed = await scenario.Get<JourneyStaging>().ReadAsync(replacement, correlation, token, SecurityScenario.Replacement);
        Assert.NotNull(seed);
        var code = FactorOracle.Code(seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Action<AdministratorInteraction>[] mutations =
        [
            row => { row.AuthorizationKind = null; row.AuthorizationVerifiedAt = null; row.SourceFactorGeneration = null; },
            row => row.AuthorizationKind = SecurityScenario.Recovery,
            row => row.AuthorizationVerifiedAt = row.ExpiresAt - 1,
            row => row.SourceFactorGeneration++,
            row => row.ExpectedEnrollmentRevision++,
            row => row.DestinationGeneration = row.SourceFactorGeneration!.Value,
            row => row.SessionPublicId = grant.SessionId,
            row => row.StagedSeed = null
        ];
        foreach (var mutation in mutations)
        {
            await RequireDeniedAsync(scenario, original, correlation, code, mutation);
        }
        Assert.NotNull(await scenario.Get<JourneyCompletion>().CompleteAsync(replacement, correlation, SecurityScenario.Replacement, code, token));
    }

    /// <summary>Restores only deliberately altered test metadata after requiring a nonmutating denial for that independent defect.</summary>
    private static async Task RequireDeniedAsync(JourneyScenario scenario, AdministratorInteraction original, string correlation,
        string code, Action<AdministratorInteraction> mutation)
    {
        var token = TestContext.Current.CancellationToken;
        var row = await scenario.Database.Set<AdministratorInteraction>().SingleAsync(value => value.Id == original.Id, token);
        mutation(row);
        await scenario.Database.SaveChangesAsync(token);
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(original.PublicId, correlation, token, SecurityScenario.Replacement));
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(original.PublicId, correlation, SecurityScenario.Replacement, code, token));
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
        var restored = await scenario.Database.Set<AdministratorInteraction>().SingleAsync(value => value.Id == original.Id, token);
        scenario.Database.Entry(restored).CurrentValues.SetValues(original);
        await scenario.Database.SaveChangesAsync(token);
        Assert.NotNull(await scenario.Get<JourneyStaging>().ReadAsync(original.PublicId, correlation, token, SecurityScenario.Replacement));
    }

    /// <summary>Neither password admission nor a valid recovery code can bypass an explicitly disabled or suspended administrator.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DisabledOrSuspendedAccount_When_RecoveryIsAttempted_Then_ExistingCodeAndAuthorityAreUnchanged(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var token = TestContext.Current.CancellationToken;
        const int ProhibitedStates = 2;
        for (var state = 0; state < ProhibitedStates; state++)
        {
            var user = await scenario.Database.Users.SingleAsync(token);
            var enrollment = await scenario.Database.Set<AdministratorEnrollment>().SingleAsync(token);
            user.IsEnabled = state != 0;
            enrollment.Suspended = state != 0;
            await scenario.Database.SaveChangesAsync(token);
            var before = await SecuritySnapshot.ReadAsync(scenario.Database);
            Assert.Null((await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password,
                SecurityScenario.Recovery, scenario.Browser, scenario.Correlation, token)).Interaction);
            Assert.Null(await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
                grant.RecoveryCodes[0], CeremonySecrets.Create(), token));
            Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
        }
    }

    /// <summary>A lost final-code stage leaves recovery blocked permanently within this increment and never reopens bootstrap.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_FinalRecoveryCode_When_ReplacementExpires_Then_PasswordCannotResumeOrReinitializeAdministrator(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var rows = await scenario.Database.Set<AdministratorRecoveryCode>().ToArrayAsync(token);
        var verifier = FactorOracle.Verifier(grant.UserId, rows[0].Generation, grant.RecoveryCodes[0]);
        foreach (var row in rows.Where(row => row.Verifier != verifier))
        {
            row.RedeemedAt = scenario.Clock.Now.ToUnixTimeSeconds();
        }
        await scenario.Database.SaveChangesAsync(token);
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var correlation = CeremonySecrets.Create();
        var lost = await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], correlation, token);
        Assert.NotNull(lost);
        const int ExpirySeconds = 300;
        scenario.Clock.Now = scenario.Clock.Now.AddSeconds(ExpirySeconds);
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(lost, correlation, token, SecurityScenario.Replacement));
        var next = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        Assert.Null(await scenario.Get<JourneyTransition>().AuthorizeAsync(next, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], CeremonySecrets.Create(), token));
        Assert.Equal(0, await scenario.Database.Set<AdministratorRecoveryCode>().CountAsync(row => row.RedeemedAt == null, token));
        Assert.True((await scenario.Database.Set<AdministratorGovernance>().AsNoTracking().SingleAsync(token)).BootstrapConsumed);
        Assert.Equal(1, await scenario.Database.Set<AdministratorBootstrapReceipt>().CountAsync(token));
        const string Enrollment = "Enrollment";
        Assert.Null((await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password, Enrollment,
            scenario.Browser, scenario.Correlation, token)).Interaction);
    }
}
