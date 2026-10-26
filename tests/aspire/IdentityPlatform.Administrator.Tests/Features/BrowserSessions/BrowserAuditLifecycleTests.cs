using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Preserves durable source history after short login expiry and cleanup without inventing anonymous ancestry.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserAuditLifecycleTests(BootstrapFixture fixture)
{
    private const string Link = "administrator-browser-audit-link";
    private const string Logout = "administrator-logout";

    /// <summary>Live-source logout after original intent expiry links its original history; subsequent production cleanup retains both audits.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ExpiredIntentAndLiveSource_When_LogoutThenContinuationCleanupRun_Then_AuditAncestrySurvives(string provider)
    {
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider);
        var journey = scenario.Login.Issuer.Account.Journey;
        await using var scope = journey.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var continuation = await database.Set<IssuerContinuation>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(continuation.ExpiresAt + 1);
        Assert.True(journey.Clock.Now.ToUnixTimeSeconds() < continuation.SessionExpiresAt);
        var form = await scenario.FormAsync();
        using var response = await scenario.PostAsync(form);
        await scenario.RequireReturnAsync(response);
        await scenario.RequireRevokedAsync();
        var before = await HistoryAsync(database);
        Assert.Equal(2, before.Length);
        journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(continuation.RetainUntil + 1);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IssuerContinuationService>().CleanupAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await database.Set<IssuerContinuation>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(before, await HistoryAsync(database));
    }

    /// <summary>A durable anonymous generation without an authenticated ticket can cancel only anonymous history.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_AnonymousPendingLogin_When_LocalLogoutCommits_Then_NoAuthenticatedSourceRelationIsInvented(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest()));
        Assert.NotNull(await scenario.LogoutAsync(browser, 1, null, null));
        await scenario.RunAsync(async (database, _) =>
        {
            var audit = await database.Set<BrowserSessionAudit>().AsNoTracking().SingleAsync(row => row.Operation == BrowserMutation.Logout,
                TestContext.Current.CancellationToken);
            Assert.Null(audit.RelatedCorrelationId);
            Assert.Null(audit.Subject);
            Assert.Null(audit.SourceSession);
            return true;
        });
    }

    /// <summary>Compares only nonsecret immutable identifiers from the two durable audit events across real cleanup.</summary>
    private static async Task<(Guid Id, Guid Operation, Guid? Related)[]> HistoryAsync(PlatformDbContext database)
    {
        var rows = await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => row.Event == Link || row.Event == Logout)
            .OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken);
        return rows.Select(row => (row.Id, row.CorrelationId, row.RelatedCorrelationId)).ToArray();
    }
}
