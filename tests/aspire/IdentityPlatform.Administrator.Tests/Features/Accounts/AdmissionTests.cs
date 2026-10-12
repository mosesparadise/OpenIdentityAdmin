using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Proves pending bootstrap eligibility, durable destination continuity and independent native failure accounting.</summary>
[Collection("administrator-bootstrap")]
public sealed class AdmissionTests(BootstrapFixture fixture)
{
    private const string Enrollment = "Enrollment";
    private const string Login = "Login";
    private const string WrongPassword = "synthetic-invalid-password";
    private const int Limit = 8;
    private const int Failures = 5;

    /// <summary>Repeated committed staging uses increasing destinations while every earlier account remains disabled and Pending.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AbandonedEnrollment_When_NewPasswordAdmissionSucceeds_Then_DestinationIsNeverReused(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        await scenario.StageAsync();
        var first = await scenario.Database.Set<AdministratorInteraction>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        await scenario.StageAsync();
        var rows = await scenario.Database.Set<AdministratorInteraction>().AsNoTracking().OrderBy(row => row.DestinationGeneration)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Length);
        Assert.Equal(first.DestinationGeneration + 1, rows[1].DestinationGeneration);
        Assert.NotEqual(rows[0].PublicId, rows[1].PublicId);
        Assert.True(rows.All(row => row.ExpectedEnrollmentRevision == 0));
        var account = await scenario.Database.Users.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(account.IsEnabled || account.TwoFactorEnabled);
        Assert.Empty(await scenario.Database.Set<AdministratorSession>().ToArrayAsync(TestContext.Current.CancellationToken));
        var state = await scenario.Database.Set<AdministratorAuthentication>().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(rows[1].DestinationGeneration, state.LastAllocatedFactorGeneration);
    }

    /// <summary>Correct password submissions between failures do not reopen the combined budget or activate the bootstrap user.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PasswordFailures_When_CorrectPasswordsIntervene_Then_FifthFailureLocksWithoutSession(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        for (var failure = 1; failure <= Failures; failure++)
        {
            var denied = await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, WrongPassword, Enrollment,
                scenario.Browser, scenario.Correlation, TestContext.Current.CancellationToken);
            Assert.Null(denied.Interaction);
            if (failure < Failures)
            {
                await scenario.StageAsync();
            }
            var state = await scenario.Database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(failure, state.Failures);
        }
        var locked = await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password, Enrollment,
            scenario.Browser, scenario.Correlation, TestContext.Current.CancellationToken);
        Assert.Null(locked.Interaction);
        Assert.Empty(await scenario.Database.Set<AdministratorSession>().ToArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>At eight live interactions admission refuses further work without clearing failures or evicting another tab.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_FullBrowserBudget_When_NinthPasswordRequestArrives_Then_ExistingInteractionsRemainIntact(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        for (var index = 0; index < Limit; index++)
        {
            await scenario.StageAsync();
        }
        var before = await scenario.Database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var denied = await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, WrongPassword, Enrollment,
            scenario.Browser, scenario.Correlation, TestContext.Current.CancellationToken);
        Assert.Null(denied.Interaction);
        Assert.False(denied.NeedsDummyWork);
        const string UnknownAccount = "unknown-synthetic-account";
        var unknown = await scenario.Get<JourneyPassword>().BeginAsync(UnknownAccount, WrongPassword, Enrollment,
            scenario.Browser, scenario.Correlation, TestContext.Current.CancellationToken);
        Assert.False(unknown.NeedsDummyWork);
        Assert.Null(unknown.Interaction);
        var ineligible = await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, WrongPassword, Login,
            scenario.Browser, scenario.Correlation, TestContext.Current.CancellationToken);
        Assert.False(ineligible.NeedsDummyWork);
        Assert.Null(ineligible.Interaction);

        var after = await scenario.Database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(before.Failures, after.Failures);
        Assert.Equal(before.LastAllocatedFactorGeneration, after.LastAllocatedFactorGeneration);
        Assert.Equal(Limit, await scenario.Database.Set<AdministratorInteraction>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await scenario.Database.Set<AdministratorBrowserBudget>().CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Pending accounts cannot use ordinary login and enabled non-enrolled accounts cannot use the bootstrap exception.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PendingBootstrap_When_WrongJourneyOrEnableStateIsUsed_Then_NoAdmissionOccurs(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        Assert.Null((await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password, Login,
            scenario.Browser, scenario.Correlation, TestContext.Current.CancellationToken)).Interaction);
        var user = await scenario.Database.Users.SingleAsync(TestContext.Current.CancellationToken);
        user.IsEnabled = true;
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null((await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password, Enrollment,
            scenario.Browser, scenario.Correlation, TestContext.Current.CancellationToken)).Interaction);
        Assert.Empty(await scenario.Database.Set<AdministratorInteraction>().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await scenario.Database.Set<AdministratorSession>().ToArrayAsync(TestContext.Current.CancellationToken));
    }
}
