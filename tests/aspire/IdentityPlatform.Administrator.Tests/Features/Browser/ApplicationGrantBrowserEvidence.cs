using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Independently observes genuinely issued native rows before and after the browser action, retaining peer and terminal-state controls.</summary>
internal static class ApplicationGrantBrowserEvidence
{
    private const string Valid = "valid";
    private const string Revoked = "revoked";
    private const string Redeemed = "redeemed";
    private const int NativeEntries = 5;
    private const int EligibleEntries = 3;
    private const int ConsumedEntries = 2;
    private const int RevocableTokens = 2;
    private const int CompletedRevision = 3;

    /// <summary>Runs the same UI journey as ordinary Aspire with production Worker registrations against the genuine issuer's provider database.</summary>
    internal static async Task RunAsync(IPage page, BrowserHttpsFixture environment, ApplicationEditorRegistration registration, string provider)
    {
        var token = TestContext.Current.CancellationToken;
        var before = await SnapshotAsync(environment);
        var owned = before.Where(row => row.Application == registration.Id).ToArray();
        Assert.Equal(NativeEntries, owned.Length);
        Assert.Equal(EligibleEntries, owned.Count(row => row.Status == Valid));
        Assert.Equal(ConsumedEntries, owned.Count(row => row.Status == Redeemed));
        await using var worker = GrantWorkerTests.CreateHost(provider, environment.Issuer.Account.Journey.Lease.RuntimeConnection, environment.Issuer.Account.Journey.Clock);
        await worker.StartAsync(token);
        var id = await ApplicationGrantJourney.RunAsync(page, environment.Profile.ClientUri, registration, WorkspacePresentation.RequireStateAsync);
        await worker.StopAsync(token);
        var after = await SnapshotAsync(environment);
        Assert.Equal(before.Length, after.Length);
        foreach (var original in before)
        {
            var current = Assert.Single(after, row => row.Authorization == original.Authorization && row.Id == original.Id);
            Assert.Equal(original.Application, current.Application);
            Assert.Equal(original.Application == registration.Id && original.Status == Valid ? Revoked : original.Status, current.Status);
        }
        await RequireJobAsync(environment, registration.Id, id);
    }

    /// <summary>Reads exact small isolated native identity/status snapshots through fresh contexts rather than trusting worker counts.</summary>
    internal static async Task<NativeRow[]> SnapshotAsync(BrowserHttpsFixture environment)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking()
            .Select(row => new NativeRow(true, row.Id!, row.Application!.Id!, row.Status!)).ToArrayAsync(token);
        var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Select(row => new NativeRow(false, row.Id!, row.Application!.Id!, row.Status!)).ToArrayAsync(token);
        return [.. authorizations, .. tokens];
    }

    /// <summary>Requires exact durable progress, approval provenance, per-revision audit and a still-disabled target after completion.</summary>
    private static async Task RequireJobAsync(BrowserHttpsFixture environment, string application, string id)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var job = await database.Set<ApplicationGrantJob>().AsNoTracking().SingleAsync(row => row.Id == Guid.Parse(id), token);
        Assert.Equal(application, job.ApplicationId);
        Assert.Equal(GrantJobState.Succeeded, job.State);
        Assert.Equal(1, job.AuthorizationCount);
        Assert.Equal(RevocableTokens, job.TokenCount);
        Assert.Equal(CompletedRevision, job.Revision);
        Assert.Equal(1, await database.Set<ApplicationGrantApproval>().CountAsync(row => row.JobId == job.Id, token));
        Assert.Equal(CompletedRevision, await database.Set<ApplicationGrantAudit>().CountAsync(row => row.JobId == job.Id, token));
        (await GrantAuditWitness.ReadAsync(database, job.Id, token)).Require();
        Assert.False((await database.Set<ApplicationPolicy>().AsNoTracking().SingleAsync(row => row.ApplicationId == application, token)).Enabled);
    }

    /// <summary>Retains only native IDs, exact application binding and status for this isolated evidence comparison.</summary>
    internal sealed record NativeRow(bool Authorization, string Id, string Application, string Status);
}
