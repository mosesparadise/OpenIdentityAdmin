using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Proves that helper composition cannot cross transaction ownership or reuse committed signing authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class OwnershipTests(BootstrapFixture fixture)
{
    private const string Login = "Login";

    /// <summary>A foreign database helper is rejected before independently committing revocations outside the owning audit transaction.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ForeignHelperContext_When_RevocationOrEligibilityIsRequested_Then_NoIndependentMutationOccurs(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        await using var foreignScope = scenario.Lease.Runtime.CreateAsyncScope();
        var foreign = foreignScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using (var transaction = await CeremonyTransaction.OpenAsync(scenario.Database, scenario.Get<NativeOperation>(),
            new(false, false, [], [grant.UserId]), TestContext.Current.CancellationToken))
        {
            var account = await CeremonyAccount.LoadAsync(scenario.Database, transaction, grant.UserId);
            await Assert.ThrowsAsync<FactorUnavailableException>(() => new JourneyRevocation(foreign).RevokeAsync(transaction, account));
            await Assert.ThrowsAsync<FactorUnavailableException>(() => new JourneyEligibility(foreign).AllowsAsync(transaction, account, Login));
        }
        Assert.False((await foreign.Set<AdministratorSession>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Revoked);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
    }

    /// <summary>Actual committed native completion can authorize only one signing attempt and exposes no callable constructor.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CommittedGrant_When_SigningAuthorityIsClaimedAgain_Then_ReuseIsRejected(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        grant.Claim();
        Assert.Throws<FactorUnavailableException>(grant.Claim);
        Assert.All(typeof(JourneyCompletion.Grant).GetConstructors(System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic), constructor => Assert.True(constructor.IsPrivate));
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
    }
    /// <summary>Native proof for one account cannot activate another account, and activation requires the declared governance lock.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_VerifiedStagedProof_When_ActivationOwnerOrGovernanceDoesNotMatch_Then_NoNativeWritesOccur(string provider)
    {
        const string SecondUsername = "second-synthetic-account";
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        await scenario.StageAsync();
        var second = new PlatformUser { UserName = SecondUsername, IsEnabled = false, LockoutEnabled = true };
        Assert.True((await scenario.Get<UserManager<PlatformUser>>().CreateAsync(second)).Succeeded);
        scenario.Database.Add(new AdministratorEnrollment { UserId = second.Id });
        scenario.Database.Add(new AdministratorAuthentication { UserId = second.Id });
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        await RequireActivationDeniedAsync(scenario, second.Id, governance: false);
        await RequireActivationDeniedAsync(scenario, second.Id, governance: true);
        Assert.Empty(await scenario.Database.Set<AdministratorFactor>().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await scenario.Database.Set<AdministratorRecoveryCode>().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.All(await scenario.Database.Users.AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken),
            user => Assert.False(user.IsEnabled || user.TwoFactorEnabled));
    }

    /// <summary>Obtains a genuine replay-reserved native proof and rejects a mismatched activation before installing its factor.</summary>
    private static async Task RequireActivationDeniedAsync(JourneyScenario scenario, string secondId, bool governance)
    {
        const string Enrollment = "Enrollment";
        var token = TestContext.Current.CancellationToken;
        var row = await scenario.Database.Set<AdministratorInteraction>().AsNoTracking().SingleAsync(token);
        await using var transaction = await CeremonyTransaction.OpenAsync(scenario.Database, scenario.Get<NativeOperation>(),
            new(governance, true, [row.BrowserBudgetId], [row.UserId, secondId]), token);
        var account = await CeremonyAccount.LoadAsync(scenario.Database, transaction, row.UserId);
        var other = await CeremonyAccount.LoadAsync(scenario.Database, transaction, secondId);
        var interaction = await CeremonyInteraction.ReadAsync(scenario.Database, transaction, account, scenario.Interaction,
            scenario.Correlation, Enrollment, scenario.Clock.Now.ToUnixTimeSeconds());
        Assert.NotNull(interaction);
        var proof = await scenario.Get<CeremonyReplay>().VerifyAsync(transaction, account, interaction,
            FactorOracle.Code(scenario.Seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), staged: true);
        Assert.NotNull(proof);
        await scenario.Get<CeremonyBudget>().CompleteAsync(transaction, account, proof);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.Get<JourneyActivation>().ActivateAsync(transaction,
            governance ? other : account, proof));
        Assert.Empty(await scenario.Database.Set<AdministratorFactor>().ToArrayAsync(token));
    }

}
