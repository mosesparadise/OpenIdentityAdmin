using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Changes authoritative state or policy time only after observing a real completion waiting for its admission lock.</summary>
[Collection("administrator-bootstrap")]
public sealed class WaitingVerificationTests(BootstrapFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private const string ProviderTrait = "Provider";
    private const string AdmissionStage = "%UPDATE%AdministratorAdmission%";
    private const string ChangedStamp = "synthetic-raced-security-stamp";
    private const int FreshnessSeconds = 300;
    private const int DrainSeconds = 40;

    public enum Change
    {
        Suspension, Disabled, Lockout, Stamp, AuthenticationGeneration, FactorGeneration, Revision, Expiry, PasswordFreshness
    }

    /// <summary>Reviews each independent invalidation on both providers rather than combining conditions that can mask one another.</summary>
    public static IEnumerable<TheoryDataRow<string, Change>> Cases =>
        from provider in Providers
        from change in Enum.GetValues<Change>()
        select new TheoryDataRow<string, Change>(provider, change).WithTrait(ProviderTrait, provider);

    /// <summary>Old challenges cannot acquire authority from post-lock state; restoring eligibility permits a genuinely fresh admission/proof.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_WaitingLoginCompletion_When_AuthorityOrFreshnessChanges_Then_StaleProofCannotCommitAndFreshAdmissionRemainsUsable(string provider, Change change)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Login);
        var code = await SecurityScenario.UnusedAsync(scenario);
        await using var owner = scenario.Lease.Administrator.CreateAsyncScope();
        var database = owner.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await database.Set<AdministratorAdmission>().ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, row => row.Revision + 1), TestContext.Current.CancellationToken);
        var blocker = await DatabaseWaiters.IdentifyAsync(database, TestContext.Current.CancellationToken);
        await using var contender = scenario.CreateScope();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var waiting = contender.ServiceProvider.GetRequiredService<JourneyCompletion>().CompleteAsync(initial, scenario.Correlation,
            SecurityScenario.Login, code, stop.Token);
        try
        {
            await DatabaseWaiters.ObserveAsync(scenario.Lease, blocker, AdmissionStage, 1);
            var originalTime = scenario.Clock.Now;
            var originals = await MutateAsync(database, scenario, initial, change);
            var afterMutation = await SecuritySnapshot.ReadAsync(database);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
            await transaction.DisposeAsync();
            Assert.Null(await waiting);
            Assert.Equal(afterMutation, await SecuritySnapshot.ReadAsync(database));
            Restore(database, originals);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            scenario.Clock.Now = originalTime;
            var fresh = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Login);
            Assert.NotNull(await scenario.Get<JourneyCompletion>().CompleteAsync(fresh, scenario.Correlation, SecurityScenario.Login,
                code, TestContext.Current.CancellationToken));
        }
        finally
        {
            await stop.CancelAsync();
            using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(DrainSeconds));
            _ = await Record.ExceptionAsync(() => waiting.WaitAsync(drain.Token));
            Assert.True(waiting.IsCompleted);
        }
    }

    /// <summary>Snapshots complete entities, changes one eligibility predicate, and retains coherent protected material for a generation change.</summary>
    private static async Task<(object Entity, PropertyValues Values)[]> MutateAsync(PlatformDbContext database, JourneyScenario scenario, string initial, Change change)
    {
        var token = TestContext.Current.CancellationToken;
        var user = await database.Users.SingleAsync(token);
        var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(token);
        var authentication = await database.Set<AdministratorAuthentication>().SingleAsync(token);
        var interaction = await database.Set<AdministratorInteraction>().SingleAsync(row => row.PublicId == initial, token);
        var factor = await database.Set<AdministratorFactor>().SingleAsync(token);
        var browser = await database.Set<AdministratorBrowserBudget>().SingleAsync(row => row.Id == interaction.BrowserBudgetId, token);
        object[] entities = [user, enrollment, authentication, interaction, factor, browser];
        var originals = entities.Select(entity => (entity, database.Entry(entity).CurrentValues.Clone())).ToArray();
        ApplyMutation(scenario, change, user, enrollment, authentication, interaction, factor, browser);
        await database.SaveChangesAsync(token);
        return originals;
    }

    /// <summary>Changes the one selected native, bound-state or clock predicate after originals have been captured for restoration.</summary>
    private static void ApplyMutation(JourneyScenario scenario, Change change, PlatformUser user, AdministratorEnrollment enrollment,
        AdministratorAuthentication authentication, AdministratorInteraction interaction, AdministratorFactor factor, AdministratorBrowserBudget browser)
    {
        switch (change)
        {
            case Change.Suspension:
                enrollment.Suspended = true;
                break;
            case Change.Disabled:
                user.IsEnabled = false;
                break;
            case Change.Lockout:
                user.LockoutEnd = scenario.Clock.Now.AddMinutes(1);
                break;
            case Change.Stamp:
                user.SecurityStamp = ChangedStamp;
                break;
            case Change.AuthenticationGeneration:
                authentication.Generation++;
                break;
            case Change.FactorGeneration:
                AdvanceFactor(scenario, enrollment, authentication, factor);
                break;
            case Change.Revision:
                enrollment.Revision++;
                break;
            case Change.Expiry:
                scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(interaction.ExpiresAt);
                break;
            case Change.PasswordFreshness:
                ExpirePassword(scenario, interaction, browser);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
    }

    /// <summary>Keeps interaction and browser validity independent while crossing only the original password-proof deadline.</summary>
    private static void ExpirePassword(JourneyScenario scenario, AdministratorInteraction interaction, AdministratorBrowserBudget browser)
    {
        interaction.ExpiresAt += FreshnessSeconds;
        browser.ExpiresAt += FreshnessSeconds;
        scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(interaction.PasswordVerifiedAt + FreshnessSeconds);
        Assert.True(browser.ExpiresAt > scenario.Clock.Now.ToUnixTimeSeconds());
    }

    /// <summary>Preserves coherent active key protection while invalidating only the old interaction's destination binding.</summary>
    private static void AdvanceFactor(JourneyScenario scenario, AdministratorEnrollment enrollment,
        AdministratorAuthentication authentication, AdministratorFactor factor)
    {
        enrollment.FactorGeneration++;
        authentication.LastAllocatedFactorGeneration = enrollment.FactorGeneration;
        factor.Generation = enrollment.FactorGeneration;
        factor.ProtectedSeed = scenario.Get<FactorProtection>().Protect(scenario.Seed, factor.UserId, factor.Generation, FactorProtection.Active);
    }

    /// <summary>Restores only the deliberately changed test state after the denied operation has completed and disposed its transaction.</summary>
    private static void Restore(PlatformDbContext database, (object Entity, PropertyValues Values)[] originals)
    {
        foreach (var (entity, values) in originals)
        {
            database.Entry(entity).CurrentValues.SetValues(values);
        }
    }
}
