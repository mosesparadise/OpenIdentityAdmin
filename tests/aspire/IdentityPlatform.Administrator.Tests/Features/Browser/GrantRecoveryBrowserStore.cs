using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Independently observes genuine browser jobs and runs production batches/Worker without creating synthetic approvals or browser authority.</summary>
internal static class GrantRecoveryBrowserStore
{
    internal const string Permission = "grants.revoke";
    private const string Valid = "valid";
    private const string Revoked = "revoked";
    private const int TokenCount = 2;
    private const int CompletionMilliseconds = 30000;
    private const string Counts = "Authorizations revoked: 1. Tokens revoked: 2.";

    /// <summary>Reads the one real browser-created job and every associated approval/audit through a fresh context.</summary>
    internal static async Task<GrantAuditWitness> ReadAsync(BrowserHttpsFixture environment, string application)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var id = await database.Set<ApplicationGrantJob>().AsNoTracking().Where(row => row.ApplicationId == application).Select(row => row.Id).SingleAsync(token);
        var result = await GrantAuditWitness.ReadAsync(database, id, token);
        result.Require();
        return result;
    }

    /// <summary>Captures protected native metadata and policy only as safe digests; no credential material enters assertion diagnostics.</summary>
    internal static async Task<ApplicationUpdateSnapshot> ConfigurationAsync(BrowserHttpsFixture environment, string application)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        return await ApplicationUpdateSnapshot.ReadAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>(), application, TestContext.Current.CancellationToken);
    }

    /// <summary>Invokes the real owned batch coordinator with its original shared clock and actual API service graph.</summary>
    internal static Task<bool> BatchAsync(BrowserHttpsFixture environment, Guid job) => GrantBatchOperation.ExecuteAsync(
        environment.Api!.Services.GetRequiredService<IServiceScopeFactory>(), job, environment.Issuer.Account.Journey.Clock, TestContext.Current.CancellationToken);

    /// <summary>Changes actual persisted permission, records a real authority stop, then proves restoring access cannot restart it.</summary>
    internal static async Task<GrantAuditWitness> StopAsync(BrowserHttpsFixture environment, string application, Guid job)
    {
        await EditorRecoveryStore.WithoutPermissionAsync(environment, Permission, async () => Assert.True(await BatchAsync(environment, job)));
        var stopped = await ReadAsync(environment, application);
        Assert.Equal(GrantJobState.Stopped, stopped.Job.State);
        Assert.Equal(GrantJobFailure.Authority, stopped.Job.Failure);
        Assert.Equal(1, stopped.Job.AuthorizationCount);
        Assert.Equal(TokenCount, stopped.Job.TokenCount);
        Assert.False(await BatchAsync(environment, job));
        await UnchangedAsync(environment, application, stopped);
        return stopped;
    }

    /// <summary>Requires a denied or replayed browser action to preserve the exact job and immutable history with no extra approval/audit.</summary>
    internal static async Task UnchangedAsync(BrowserHttpsFixture environment, string application, GrantAuditWitness before)
    {
        var after = await ReadAsync(environment, application);
        after.Preserves(before);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Job), ApplicationUpdateSnapshot.Hash(after.Job));
        Assert.Equal(before.Approvals.Length, after.Approvals.Length);
        Assert.Equal(before.Details.Length, after.Details.Length);
    }

    /// <summary>Runs actual hosted Worker composition until the browser observes protected completion; shutdown always joins before test teardown.</summary>
    internal static async Task CompleteAsync(IPage page, BrowserHttpsFixture environment, string provider)
    {
        var token = TestContext.Current.CancellationToken;
        await using var worker = GrantWorkerTests.CreateHost(provider, environment.Issuer.Account.Journey.Lease.RuntimeConnection, environment.Issuer.Account.Journey.Clock);
        await worker.StartAsync(token);
        try
        {
            await Assertions.Expect(page.GetByText(GrantRecoveryView.Completed, new()
            {
                Exact = true
            }))
                .ToBeVisibleAsync(new()
                {
                    Timeout = CompletionMilliseconds
                });
        }
        finally { await worker.StopAsync(token); }
        await Assertions.Expect(page.GetByText(Counts, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
    }

    /// <summary>Checks every original native row and the target configuration, not only displayed counters or job status.</summary>
    internal static async Task RequireNativeAsync(BrowserHttpsFixture environment, string application,
        ApplicationGrantBrowserEvidence.NativeRow[] before, ApplicationUpdateSnapshot configuration)
    {
        var after = await ApplicationGrantBrowserEvidence.SnapshotAsync(environment);
        Assert.Equal(before.Length, after.Length);
        foreach (var row in before)
        {
            var expected = row.Application == application && row.Status == Valid ? row with
            {
                Status = Revoked
            } : row;
            Assert.Equal(expected, Assert.Single(after, current => current.Authorization == row.Authorization && current.Id == row.Id));
        }
        var current = await ConfigurationAsync(environment, application);
        Assert.Equal(configuration.NativeDigest, current.NativeDigest);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(configuration.Policy), ApplicationUpdateSnapshot.Hash(current.Policy));
        Assert.Equal(ApplicationUpdateSnapshot.Hash(configuration.LifecycleAudits), ApplicationUpdateSnapshot.Hash(current.LifecycleAudits));
        Assert.False(current.Policy.Enabled);
    }
}
