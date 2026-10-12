using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Checks the permanent bootstrap exception without equating a valid password or setup key with current eligibility.</summary>
[Collection("administrator-bootstrap")]
public sealed class EnrollmentEligibilityTests(BootstrapFixture fixture)
{
    private const string Enrollment = "Enrollment";
    private const string WrongOwner = "synthetic-wrong-owner";

    /// <summary>Wrong receipt ownership, unconsumed governance, suspension and legacy revision bindings independently deny setup disclosure and activation.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PendingEnrollment_When_OwnershipOrRevisionChanges_Then_OnlyRestoredExactStateCanProceed(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        await scenario.StageAsync();
        await RequireMutationAsync<AdministratorBootstrapReceipt>(scenario, row => row.UserId = WrongOwner);
        await RequireMutationAsync<AdministratorGovernance>(scenario, row => row.BootstrapConsumed = false);
        await RequireMutationAsync<AdministratorEnrollment>(scenario, row => row.Suspended = true);
        await RequireMutationAsync<AdministratorInteraction>(scenario, row => row.ExpectedEnrollmentRevision = null);
        var code = FactorOracle.Code(scenario.Seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.NotNull(await scenario.Get<JourneyCompletion>().CompleteAsync(scenario.Interaction, scenario.Correlation, Enrollment, code, TestContext.Current.CancellationToken));
    }

    /// <summary>A missing permanent receipt denies an otherwise valid pending ceremony without recreating bootstrap ownership.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PendingEnrollment_When_PermanentReceiptIsMissing_Then_NoAuthorityIsReconstructed(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        await scenario.StageAsync();
        await scenario.Database.Set<AdministratorBootstrapReceipt>().ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await RequireDeniedAsync(scenario);
        Assert.Empty(await scenario.Database.Set<AdministratorBootstrapReceipt>().ToArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Restores the exact independently loaded row and proves subsequent eligibility before testing another mutation.</summary>
    private static async Task RequireMutationAsync<T>(JourneyScenario scenario, Action<T> mutate) where T : class
    {
        var token = TestContext.Current.CancellationToken;
        var row = await scenario.Database.Set<T>().SingleAsync(token);
        var snapshot = scenario.Database.Entry(row).CurrentValues.Clone();
        mutate(row);
        await scenario.Database.SaveChangesAsync(token);
        await RequireDeniedAsync(scenario);
        row = await scenario.Database.Set<T>().SingleAsync(token);
        scenario.Database.Entry(row).CurrentValues.SetValues(snapshot);
        await scenario.Database.SaveChangesAsync(token);
        Assert.True(scenario.Seed == await scenario.Get<JourneyStaging>().ReadAsync(scenario.Interaction, scenario.Correlation, token));
    }

    /// <summary>Both read-only staging and correct native proof are denied without installing a factor or creating a session.</summary>
    private static async Task RequireDeniedAsync(JourneyScenario scenario)
    {
        var token = TestContext.Current.CancellationToken;
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(scenario.Interaction, scenario.Correlation, token));
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(scenario.Interaction, scenario.Correlation, Enrollment,
            FactorOracle.Code(scenario.Seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), token));
        Assert.Empty(await scenario.Database.Set<AdministratorFactor>().ToArrayAsync(token));
        Assert.Empty(await scenario.Database.Set<AdministratorSession>().ToArrayAsync(token));
    }
}
