using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Proves current stored authority and exact session timing without manufacturing authentication claims.</summary>
[Collection("administrator-bootstrap")]
public sealed class SessionTests(BootstrapFixture fixture)
{
    private const int IdleSeconds = 1800;
    private const int AbsoluteSeconds = 28800;
    private const int FailureLimit = 5;
    private const string Active = "Active";
    private const string RecoveryRequired = "RecoveryRequired";

    /// <summary>At exact idle expiry the session denies; activity before expiry preserves original proof and absolute times.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CommittedSession_When_IdleBoundaryIsReached_Then_AuthorityExpiresWithoutChangingProofTimes(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var original = await scenario.Database.Set<AdministratorSession>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(original.CreatedAt + IdleSeconds - 1);
        var status = await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken);
        Assert.NotNull(status);
        Assert.Equal(original.PasswordVerifiedAt, status.PasswordVerifiedAt);
        Assert.Equal(original.FactorVerifiedAt, status.FactorVerifiedAt);
        Assert.Equal(original.ExpiresAt, status.ExpiresAt);
        scenario.Clock.Now = scenario.Clock.Now.AddSeconds(IdleSeconds);
        Assert.Null(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
        var final = await scenario.Database.Set<AdministratorSession>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(original.CreatedAt + IdleSeconds - 1, final.LastActivityAt);
        Assert.Equal(1, final.Revision);
    }

    /// <summary>Even continuously recent activity cannot extend the immutable eight-hour deadline.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RecentActivity_When_AbsoluteBoundaryIsReached_Then_SessionCannotRenew(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var session = await scenario.Database.Set<AdministratorSession>().SingleAsync(TestContext.Current.CancellationToken);
        session.LastActivityAt = session.CreatedAt + AbsoluteSeconds - 1;
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(session.ExpiresAt - 1);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
        scenario.Clock.Now = scenario.Clock.Now.AddSeconds(1);
        Assert.Null(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
        var final = await scenario.Database.Set<AdministratorSession>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(grant.ExpiresAt - 1, final.LastActivityAt);
    }

    /// <summary>Current account and enrollment prohibitions are immediately enforced, then a restored control is accepted.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExistingCookieAuthority_When_CurrentAccountBecomesIneligible_Then_EachRequestDenies(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        await AssertMutationAsync(scenario, grant, user => user.IsEnabled = false, user => user.IsEnabled = true);
        await AssertMutationAsync(scenario, grant, user => user.TwoFactorEnabled = false, user => user.TwoFactorEnabled = true);
        await AssertMutationAsync(scenario, grant, user => user.LockoutEnabled = false, user => user.LockoutEnabled = true);
        await AssertMutationAsync(scenario, grant, user => user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1), user => user.LockoutEnd = null);
        await AssertEnrollmentAsync(scenario, grant, enrollment => enrollment.Suspended = true, enrollment => enrollment.Suspended = false);
        await AssertEnrollmentAsync(scenario, grant, enrollment => enrollment.State = RecoveryRequired, enrollment => enrollment.State = Active);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
    }

    /// <summary>Expired product lockout may permit current sessions but validation cannot reset failures or native counters.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExpiredFailureBudget_When_SessionIsValidated_Then_OnlyActivityMetadataChanges(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var grant = await scenario.EnrollAsync();
        var authentication = await scenario.Database.Set<AdministratorAuthentication>().SingleAsync(TestContext.Current.CancellationToken);
        authentication.Failures = FailureLimit;
        authentication.LockoutUntil = scenario.Clock.Now.ToUnixTimeSeconds() - 1;
        var user = await scenario.Database.Users.SingleAsync(TestContext.Current.CancellationToken);
        user.AccessFailedCount = FailureLimit;
        var audits = await scenario.Database.Set<SecurityAuditIntent>().CountAsync(TestContext.Current.CancellationToken);
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
        authentication = await scenario.Database.Set<AdministratorAuthentication>().SingleAsync(TestContext.Current.CancellationToken);
        user = await scenario.Database.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(FailureLimit, authentication.Failures);
        Assert.Equal(FailureLimit, user.AccessFailedCount);
        Assert.NotNull(authentication.LockoutUntil);
        Assert.Equal(audits, await scenario.Database.Set<SecurityAuditIntent>().CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Applies one independent native-state mutation and proves denial before restoring that exact field.</summary>
    private static async Task AssertMutationAsync(JourneyScenario scenario, JourneyCompletion.Grant grant,
        Action<PlatformUser> mutate, Action<PlatformUser> restore)
    {
        var user = await scenario.Database.Users.SingleAsync(TestContext.Current.CancellationToken);
        mutate(user);
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
        user = await scenario.Database.Users.SingleAsync(TestContext.Current.CancellationToken);
        restore(user);
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
    }

    /// <summary>Checks current enrollment independently of the native enable flag and existing session ticket.</summary>
    private static async Task AssertEnrollmentAsync(JourneyScenario scenario, JourneyCompletion.Grant grant,
        Action<AdministratorEnrollment> mutate, Action<AdministratorEnrollment> restore)
    {
        var enrollment = await scenario.Database.Set<AdministratorEnrollment>().SingleAsync(TestContext.Current.CancellationToken);
        mutate(enrollment);
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
        enrollment = await scenario.Database.Set<AdministratorEnrollment>().SingleAsync(TestContext.Current.CancellationToken);
        restore(enrollment);
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
    }
}
