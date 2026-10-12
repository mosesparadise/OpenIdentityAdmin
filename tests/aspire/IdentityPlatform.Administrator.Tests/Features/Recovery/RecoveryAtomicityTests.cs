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

namespace IdentityPlatform.Administrator.Tests.Features.Recovery;

/// <summary>Requires native redemption, invalidation, stage rotation and audits to roll back as one operation on every failure boundary.</summary>
[Collection("administrator-bootstrap")]
public sealed class RecoveryAtomicityTests(BootstrapFixture fixture)
{
    private const string NativeFailureMarker = "administrator-recovery-rollback-bypass";

    /// <summary>A native validation error after real redemption must not be misclassified as a normal denial that spends the verifier.</summary>
    [Trait("CriticalDecision", "administrator-recovery-rollback")]
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ClaimedRecoveryVerifier_When_NativeUserUpdateFails_Then_EntireClaimAndAuthorityRollBack(string provider)
    {
        var failure = new RecoveryUpdateFailure();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider, services => services.AddSingleton<IUserValidator<PlatformUser>>(failure));
        var grant = await scenario.EnrollAsync();
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        failure.Database = scenario.Database;
        failure.Armed = true;
        var error = await Record.ExceptionAsync(() => scenario.Get<JourneyTransition>().AuthorizeAsync(initial,
            scenario.Correlation, SecurityScenario.Recovery, grant.RecoveryCodes[0], CeremonySecrets.Create(), TestContext.Current.CancellationToken));
        Assert.True(error is null or FactorUnavailableException);
        Assert.True(failure.ObservedClaim);
        Assert.True(before == await SecuritySnapshot.ReadAsync(scenario.Database), NativeFailureMarker);
        Assert.IsType<FactorUnavailableException>(error);
        failure.Armed = false;
        Assert.NotNull(await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], CeremonySecrets.Create(), TestContext.Current.CancellationToken));
    }

    /// <summary>Denied audit storage restores every native/product field and leaves the original code and binding usable once.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ValidRecoveryProof_When_AuditWriteFails_Then_RedemptionRevocationAndReplacementAllRollBack(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var token = TestContext.Current.CancellationToken;
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        await scenario.Lease.AuditPermissionAsync(false, token);
        try
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => scenario.Get<JourneyTransition>().AuthorizeAsync(initial,
                scenario.Correlation, SecurityScenario.Recovery, grant.RecoveryCodes[0], CeremonySecrets.Create(), token));
            RequirePermissionFailure(failure);
        }
        finally { await scenario.Lease.AuditPermissionAsync(true, token); }
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
        Assert.NotNull(await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], CeremonySecrets.Create(), token));
    }

    /// <summary>Crossing absolute password/interaction freshness after the actual conditional claim rolls back the spent code.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RecoveryNearExpiry_When_ConditionalClaimCrossesDeadline_Then_CodeAndAllAuthorityRemainUnchanged(string provider)
    {
        var fence = new JourneyClockFence();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fence)));
        var grant = await scenario.EnrollAsync();
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        const string Table = "AdministratorRecoveryCode";
        const string Update = "UPDATE";
        const int FreshnessSeconds = 300;
        fence.Arm(Table, () => scenario.Clock.Now = scenario.Clock.Now.AddSeconds(FreshnessSeconds), Update);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.Get<JourneyTransition>().AuthorizeAsync(initial,
            scenario.Correlation, SecurityScenario.Recovery, grant.RecoveryCodes[0], CeremonySecrets.Create(), TestContext.Current.CancellationToken));
        Assert.True(fence.Triggered);
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
    }

    /// <summary>Cancellation after native conditional redemption propagates without committing a claim, audit or replacement response.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RecoveryClaim_When_RequestIsCancelled_Then_NoPartialClaimOrRestrictedAuthorityCommits(string provider)
    {
        var fence = new JourneyClockFence();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fence)));
        var grant = await scenario.EnrollAsync();
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        const string Table = "AdministratorRecoveryCode";
        const string Update = "UPDATE";
        fence.Arm(Table, cancellation.Cancel, Update);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scenario.Get<JourneyTransition>().AuthorizeAsync(initial,
            scenario.Correlation, SecurityScenario.Recovery, grant.RecoveryCodes[0], CeremonySecrets.Create(), cancellation.Token));
        Assert.True(fence.Triggered);
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
    }

    /// <summary>Requires the actual provider permission error so an unrelated implementation exception cannot satisfy the rollback test.</summary>
    internal static void RequirePermissionFailure(DbUpdateException failure)
    {
        const string PostgreSqlPermission = "42501";
        const int SqlServerPermission = 229;
        Assert.True(failure.InnerException is Npgsql.PostgresException { SqlState: PostgreSqlPermission } ||
            failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: SqlServerPermission });
    }
}
