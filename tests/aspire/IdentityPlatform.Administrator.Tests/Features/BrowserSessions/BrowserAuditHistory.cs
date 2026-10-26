using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Checks actual cross-store audit ancestry without treating redirects as proof of issuer commit.</summary>
internal static class BrowserAuditHistory
{
    private const string Link = "administrator-browser-audit-link";
    private const string Logout = "administrator-logout";
    private const string Actor = "protocol-client";
    private const string Outcome = "accepted";
    private const string Target = "administrator-browser-login";
    private const string Client = "identity-admin-bff";

    /// <summary>Joins the real committed BFF login to its issuer continuation and exact-source logout through finite typed audit fields.</summary>
    internal static async Task RequireIssuerLogoutAsync(IssuerLogoutFixture scenario)
    {
        var local = await scenario.Login.ReadAsync(db => db.Set<BrowserSessionAudit>().AsNoTracking()
            .SingleAsync(row => row.Operation == BrowserMutation.Establish, TestContext.Current.CancellationToken));
        await using var scope = scenario.Login.Issuer.Account.Journey.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var continuation = await database.Set<IssuerContinuation>().AsNoTracking().SingleAsync(row => row.SessionId == scenario.Source,
            TestContext.Current.CancellationToken);
        var link = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(row => row.Event == Link,
            TestContext.Current.CancellationToken);
        var logout = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(row => row.Event == Logout,
            TestContext.Current.CancellationToken);
        Assert.Equal(local.CorrelationId, link.CorrelationId);
        Assert.Equal(continuation.CorrelationId, link.RelatedCorrelationId);
        Assert.Equal(continuation.CorrelationId, logout.RelatedCorrelationId);
        Assert.NotEqual(logout.CorrelationId, logout.RelatedCorrelationId);
        Assert.Equal(Client, link.ActorId);
        Assert.Equal(Actor, link.ActorType);
        Assert.Equal(Outcome, link.Outcome);
        Assert.Equal(Target, link.TargetType);
        Assert.Equal(continuation.ObservedAt, link.OccurredAt);
        Assert.Null(link.TargetId);
    }
}
