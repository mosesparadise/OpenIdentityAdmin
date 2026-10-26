using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Independently verifies original source authority after actual logout persistence, without a confirmation deadline masking the source guard.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerLogoutDeadlineTests(BootstrapFixture fixture)
{
    private const int IdleSeconds = 1800;
    private const string ProviderTrait = "Provider";
    private const string AuditEvent = "administrator-logout";
    private static readonly bool[] Variants = [false, true];

    /// <summary>Targets original idle and absolute source deadlines on each actual provider.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                    from absolute in Variants
                                                                    select new TheoryDataRow<string, bool>(provider, absolute).WithTrait(ProviderTrait, provider);

    /// <summary>A real source expiring during save rolls back both revision and audit; removing the fault lets the same genuine source revoke once.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_CurrentSource_When_OriginalAuthorityExpiresDuringLogoutSave_Then_RevocationRevisionAndAuditRollBack(string provider, bool absolute)
    {
        var fault = new IssuerLogoutFault();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fault)));
        var grant = await scenario.EnrollAsync();
        var original = await scenario.Database.Set<AdministratorSession>().SingleAsync(TestContext.Current.CancellationToken);
        var boundary = absolute ? original.ExpiresAt : original.LastActivityAt + IdleSeconds;
        if (absolute)
        {
            original.LastActivityAt = boundary - 1;
            await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(boundary - 1);
        fault.AfterSave = () => scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(boundary);
        var candidate = new JourneySessionCandidate(grant.UserId, grant.SessionId);
        await using (var scope = scenario.CreateScope())
        {
            await Assert.ThrowsAsync<FactorUnavailableException>(() => scope.ServiceProvider.GetRequiredService<JourneyLogout>().RevokeAsync(candidate, TestContext.Current.CancellationToken));
        }
        Assert.Equal(1, fault.Saves);
        Assert.Equal(0, fault.Commits);
        var denied = await scenario.Database.Set<AdministratorSession>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(denied.Revoked);
        Assert.Equal(original.Revision, denied.Revision);
        Assert.Equal(original.LastActivityAt, denied.LastActivityAt);
        Assert.Equal(0, await scenario.Database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == AuditEvent, TestContext.Current.CancellationToken));
        fault.AfterSave = null;
        scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(boundary - 1);
        await using var control = scenario.CreateScope();
        Assert.True(await control.ServiceProvider.GetRequiredService<JourneyLogout>().RevokeAsync(candidate, TestContext.Current.CancellationToken));
        Assert.Equal(1, fault.Commits);
        var committed = await scenario.Database.Set<AdministratorSession>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(committed.Revoked);
        Assert.Equal(original.Revision + 1, committed.Revision);
        Assert.Equal(1, await scenario.Database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == AuditEvent, TestContext.Current.CancellationToken));
        Assert.Null((await scenario.Database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(row => row.Event == AuditEvent,
            TestContext.Current.CancellationToken)).RelatedCorrelationId);
    }
}
