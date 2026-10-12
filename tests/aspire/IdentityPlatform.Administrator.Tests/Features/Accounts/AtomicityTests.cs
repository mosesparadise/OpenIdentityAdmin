using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Proves failed completion never partially activates a user, spends proof or releases recovery material.</summary>
[Collection("administrator-bootstrap")]
public sealed class AtomicityTests(BootstrapFixture fixture)
{
    private const string Enrollment = "Enrollment";
    private const string Login = "Login";
    private const string Pending = "Pending";
    private const string PostgreSqlPermission = "42501";
    private const int SqlServerPermission = 229;

    /// <summary>A real audit permission failure rolls back native installation and replay; restoring permission permits the same proof once.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AuditWriteDenied_When_EnrollmentCompletes_Then_AllAuthorityRollsBackAndOriginalProofRemainsUsable(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        await scenario.StageAsync();
        var token = TestContext.Current.CancellationToken;
        var original = await scenario.Database.Users.AsNoTracking().SingleAsync(token);
        var code = FactorOracle.Code(scenario.Seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await scenario.Lease.AuditPermissionAsync(false, token);
        try
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => scenario.Get<JourneyCompletion>().CompleteAsync(
                scenario.Interaction, scenario.Correlation, Enrollment, code, token));
            Assert.True(failure.InnerException is Npgsql.PostgresException { SqlState: PostgreSqlPermission } ||
                failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: SqlServerPermission });
        }
        finally { await scenario.Lease.AuditPermissionAsync(true, token); }
        await AssertPendingAsync(scenario, original.SecurityStamp!);
        Assert.NotNull(await scenario.Get<JourneyCompletion>().CompleteAsync(scenario.Interaction, scenario.Correlation, Enrollment, code, token));
    }

    /// <summary>Stale revision bindings cannot activate even with a correct native factor and browser cookie.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_StaleEnrollmentRevision_When_CorrectFactorIsSubmitted_Then_ActivationIsDenied(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        await scenario.StageAsync();
        var token = TestContext.Current.CancellationToken;
        var original = await scenario.Database.Users.AsNoTracking().SingleAsync(token);
        var enrollment = await scenario.Database.Set<AdministratorEnrollment>().SingleAsync(token);
        enrollment.Revision++;
        await scenario.Database.SaveChangesAsync(token);
        var code = FactorOracle.Code(scenario.Seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(scenario.Interaction, scenario.Correlation, Enrollment, code, token));
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(scenario.Interaction, scenario.Correlation, token));
        await AssertPendingAsync(scenario, original.SecurityStamp!);
    }

    /// <summary>The accepted enrollment code stays reserved against the installed generation and cannot immediately authenticate again.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_InstalledEnrollmentProof_When_CodeIsReplayedForLogin_Then_OnlyNewNativeCodeCanCreateSession(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        var accepted = await scenario.Database.Set<AdministratorReplay>().SingleAsync(token);
        var used = scenario.Get<FactorProtection>().Unprotect(accepted.ProtectedCode, grant.UserId, accepted.Generation, FactorProtection.Replay);
        var login = await scenario.Get<JourneyPassword>().BeginAsync(scenario.Lease.Username, scenario.Lease.Password, Login,
            scenario.Browser, scenario.Correlation, token);
        Assert.NotNull(login.Interaction);
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(login.Interaction, scenario.Correlation, Login, used, token));
        Assert.Equal(1, await scenario.Database.Set<AdministratorSession>().CountAsync(token));
        var fresh = FactorOracle.UnusedCode(scenario.Seed, used);
        Assert.NotNull(await scenario.Get<JourneyCompletion>().CompleteAsync(login.Interaction, scenario.Correlation, Login, fresh, token));
        Assert.Equal(2, await scenario.Database.Set<AdministratorSession>().CountAsync(token));
        Assert.Equal(0, (await scenario.Database.Set<AdministratorAuthentication>().SingleAsync(token)).Failures);
    }

    /// <summary>Asserts rollback independently across native user, factor, replay, session, recovery and enrollment storage.</summary>
    private static async Task AssertPendingAsync(JourneyScenario scenario, string stamp)
    {
        var token = TestContext.Current.CancellationToken;
        scenario.Database.ChangeTracker.Clear();
        var user = await scenario.Database.Users.AsNoTracking().SingleAsync(token);
        Assert.False(user.IsEnabled || user.TwoFactorEnabled);
        Assert.True(stamp == user.SecurityStamp);
        Assert.Equal(Pending, (await scenario.Database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(token)).State);
        Assert.Empty(await scenario.Database.Set<AdministratorSession>().ToArrayAsync(token));
        Assert.Empty(await scenario.Database.Set<AdministratorFactor>().ToArrayAsync(token));
        Assert.Empty(await scenario.Database.Set<AdministratorReplay>().ToArrayAsync(token));
        Assert.Empty(await scenario.Database.Set<AdministratorRecoveryCode>().ToArrayAsync(token));
    }
}
