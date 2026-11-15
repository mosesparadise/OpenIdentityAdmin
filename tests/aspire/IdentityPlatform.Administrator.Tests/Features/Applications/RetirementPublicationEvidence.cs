using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Independently observes committed retirement and changes only durable browser state while genuine upstream output is held.</summary>
internal static class RetirementPublicationEvidence
{
    private const int ShortSeconds = 1;
    private const int ExtendedSeconds = 60;
    private const string SessionPath = "/bff/session";
    private const string Authenticated = "authenticated";
    private const string RetiredEvent = "ApplicationRetired";

    /// <summary>Proves terminal admission and its exact original approval/job/audit exist before browser authority changes.</summary>
    internal static async Task<Commit> ObserveAsync(ApplicationHttpFixture test, string id, string version, string key)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var receipt = await database.Set<ApplicationRetirement>().AsNoTracking().SingleAsync(token);
        Assert.Equal(id, receipt.ApplicationId);
        Assert.Equal(Guid.Parse(key), receipt.RequestKey);
        Assert.Equal(test.Environment.Issuer.Account.ActorId, receipt.ActorId);
        Assert.Equal(ApplicationVersion.Create(id, receipt.OriginalRevision), version);
        var policy = await database.Set<ApplicationPolicy>().AsNoTracking().SingleAsync(token);
        Assert.True(policy.Retired && !policy.Enabled);
        Assert.Equal(receipt.OriginalRevision + 1, policy.Revision);
        Assert.Equal(receipt.RetiredRevision, policy.Revision);
        var audit = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(row => row.Id == receipt.AuditId, token);
        Assert.Equal(RetiredEvent, audit.Event);
        Assert.Equal(receipt.ActorId, audit.ActorId);
        Assert.Equal(id, audit.TargetId);
        Assert.Equal(receipt.RetiredAt, audit.OccurredAt);
        var history = await GrantAuditWitness.ReadAsync(database, receipt.GrantJobId, token);
        history.Require();
        Assert.Equal(receipt.GrantApprovalId, Assert.Single(history.Approvals).Id);
        Assert.Single(history.Audits);
        Assert.Single(history.Details);
        Assert.Equal(GrantJobState.Queued, history.Job.State);
        Assert.Equal(0, history.Job.AuthorizationCount);
        Assert.Equal(0, history.Job.TokenCount);
        return new(receipt, await SnapshotAsync(test, id));
    }

    /// <summary>Includes every retained native/grant/audit field and immutable retirement receipt in before-publication and after-denial comparisons.</summary>
    internal static async Task<string> SnapshotAsync(ApplicationHttpFixture test, string id)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return ApplicationUpdateSnapshot.Hash(new
        {
            Grants = await GrantBoundaryEvidence.SnapshotAsync(database, id),
            Receipts = await database.Set<ApplicationRetirement>().AsNoTracking().OrderBy(row => row.ApplicationId).ToArrayAsync(TestContext.Current.CancellationToken)
        });
    }

    /// <summary>Establishes a short original browser lifetime while preserving the actual issuer credential and ordinary elapsed budget.</summary>
    internal static async Task ShortenAsync(ApplicationHttpFixture test)
    {
        await using var scope = test.Environment.Client.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<BrowserContext>()
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.ExpiresAt, test.Now.AddSeconds(ShortSeconds).UtcTicks), TestContext.Current.CancellationToken));
    }

    /// <summary>Changes generation, or extends current persisted lifetime before expiring only the original held request; current authentication remains a separate positive control.</summary>
    internal static async Task ChangeAsync(ApplicationHttpFixture test, bool generation)
    {
        await using var scope = test.Environment.Client.Services.CreateAsyncScope();
        var rows = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<BrowserContext>();
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(1, generation
            ? await rows.ExecuteUpdateAsync(update => update.SetProperty(row => row.Generation, row => row.Generation + 1), token)
            : await rows.ExecuteUpdateAsync(update => update.SetProperty(row => row.ExpiresAt, test.Now.AddSeconds(ExtendedSeconds).UtcTicks), token));
        if (generation)
        {
            return;
        }
        test.Environment.Issuer.Account.Journey.Clock.Now = test.Now.AddSeconds(ShortSeconds);
        using var response = await test.Browser.GetAsync(SessionPath, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var status = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.True(status.RootElement.GetProperty(Authenticated).GetBoolean());
    }

    /// <summary>Retains only safe receipt identity and the independent persisted-state digest captured before changing browser authority.</summary>
    internal sealed record Commit(ApplicationRetirement Receipt, string Snapshot);
}
