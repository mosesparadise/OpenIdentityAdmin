using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Features.Recovery;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Security;

/// <summary>Proves audit and final-freshness failures cannot partially replace factors, verifiers or session authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class SecurityAtomicityTests(BootstrapFixture fixture)
{
    /// <summary>Audit denial rolls back actual new-factor installation and preserves the original bound confirmation proof.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ProvenReplacement_When_AuditFails_Then_FactorCodesSessionsAndProofAllRollBack(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var correlation = CeremonySecrets.Create();
        var replacement = await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], correlation, token);
        Assert.NotNull(replacement);
        var seed = await scenario.Get<JourneyStaging>().ReadAsync(replacement, correlation, token, SecurityScenario.Replacement);
        Assert.NotNull(seed);
        var code = FactorOracle.Code(seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        await scenario.Lease.AuditPermissionAsync(false, token);
        try
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => scenario.Get<JourneyCompletion>().CompleteAsync(replacement,
                correlation, SecurityScenario.Replacement, code, token));
            RecoveryAtomicityTests.RequirePermissionFailure(failure);
        }
        finally { await scenario.Lease.AuditPermissionAsync(true, token); }
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
        Assert.NotNull(await scenario.Get<JourneyCompletion>().CompleteAsync(replacement, correlation, SecurityScenario.Replacement, code, token));
    }

    /// <summary>Failed regeneration audit preserves exact old verifier values, replay, session and interaction state.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ValidRegeneration_When_AuditFails_Then_AllOldAuthorityAndUnspentProofRemain(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Regeneration, grant);
        var code = await SecurityScenario.UnusedAsync(scenario);
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        await scenario.Lease.AuditPermissionAsync(false, token);
        try
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => scenario.Get<JourneyCompletion>().CompleteAsync(initial,
                scenario.Correlation, SecurityScenario.Regeneration, code, token, SecurityScenario.Candidate(grant)));
            RecoveryAtomicityTests.RequirePermissionFailure(failure);
        }
        finally { await scenario.Lease.AuditPermissionAsync(true, token); }
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
        Assert.NotNull(await scenario.Get<JourneyCompletion>().CompleteAsync(initial, scenario.Correlation, SecurityScenario.Regeneration,
            code, token, SecurityScenario.Candidate(grant)));
    }

    /// <summary>Crossing password freshness after the actual recovery INSERT withholds every new verifier and replacement session.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RegenerationNativeWrite_When_FinalFreshnessExpires_Then_NoNewCodesOrAuthorityCommit(string provider)
    {
        var fence = new JourneyClockFence();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fence)));
        var grant = await scenario.EnrollAsync();
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Regeneration, grant);
        var code = await SecurityScenario.UnusedAsync(scenario);
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        const string Table = "AdministratorRecoveryCode";
        const string Insert = "INSERT";
        const int FreshnessSeconds = 300;
        fence.Arm(Table, () => scenario.Clock.Now = scenario.Clock.Now.AddSeconds(FreshnessSeconds), Insert);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.Get<JourneyCompletion>().CompleteAsync(initial,
            scenario.Correlation, SecurityScenario.Regeneration, code, TestContext.Current.CancellationToken, SecurityScenario.Candidate(grant)));
        Assert.True(fence.Triggered);
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
    }

    /// <summary>The initiating session's expiry still applies after native writes intentionally rotate its credential generation.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_SessionNearAbsoluteExpiry_When_RegenerationWriteCrossesDeadline_Then_AllCredentialWritesRollBack(string provider)
    {
        var fence = new JourneyClockFence();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fence)));
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        const int BeforeExpirySeconds = 1;
        const int CrossExpirySeconds = 2;
        scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(grant.ExpiresAt - BeforeExpirySeconds);
        var activity = scenario.Clock.Now.ToUnixTimeSeconds();
        await scenario.Database.Set<IdentityPlatform.Infrastructure.Features.Identity.Persistence.AdministratorSession>()
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.LastActivityAt, activity), token);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Regeneration, grant);
        var code = await SecurityScenario.UnusedAsync(scenario);
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        const string Table = "AdministratorRecoveryCode";
        const string Insert = "INSERT";
        fence.Arm(Table, () => scenario.Clock.Now = scenario.Clock.Now.AddSeconds(CrossExpirySeconds), Insert);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.Get<JourneyCompletion>().CompleteAsync(initial,
            scenario.Correlation, SecurityScenario.Regeneration, code, token, SecurityScenario.Candidate(grant)));
        Assert.True(fence.Triggered);
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
    }

    /// <summary>Audit failure restores exact stored session authority; a later explicit logout can revoke it once.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CurrentSession_When_LogoutAuditFails_Then_StoredSessionRemainsUsable(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        await scenario.Lease.AuditPermissionAsync(false, token);
        try
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => scenario.Get<JourneyLogout>().RevokeAsync(SecurityScenario.Candidate(grant), token));
            RecoveryAtomicityTests.RequirePermissionFailure(failure);
        }
        finally { await scenario.Lease.AuditPermissionAsync(true, token); }
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, token));
        Assert.True(await scenario.Get<JourneyLogout>().RevokeAsync(SecurityScenario.Candidate(grant), token));
    }
}
