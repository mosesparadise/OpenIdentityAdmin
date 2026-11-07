using System.Net;
using System.Net.Http.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.ApplicationGrants.GrantBoundaryEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Checks real HTTP output after durable grant commit and actual owned cleanup, retaining original authority and elapsed request bounds.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantPublicationTests(BootstrapFixture fixture)
{
    private const string Cleanup = "cleanup";
    private const string Deadline = "deadline";
    private const string Authority = "authority";
    private const int BeyondBudgetMilliseconds = 5500;
    private const int WatchdogSeconds = 30;
    private const string CleanupNotReached = "The actual provider cleanup boundary was not reached.";
    private const string CleanupFailure = "Synthetic grant cleanup failure.";

    /// <summary>Failed or late cleanup cannot publish acceptance; durable work is preserved exactly once and eligible explicit same-key recovery does not duplicate approval.</summary>
    [Theory]
    [InlineData("PostgreSql", Cleanup, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Deadline, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Authority, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Cleanup, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Deadline, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Authority, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CommittedGrant_When_CleanupFailsOrOriginalPublicationBoundsEnd_Then_HttpsWithholdsAcceptanceAndPreservesOneJob(string provider, string boundary)
    {
        using var probe = new GrantPublicationProbe();
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider, configureApi: probe.Configure);
        var (id, version) = await GrantHttpTests.DisabledAsync(test);
        var key = Guid.NewGuid().ToString(GuidFormat);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await ConfigureAsync(test, probe, boundary, entered, release);
        probe.Armed = true;
        var pending = test.GrantAsync(id, Body, version, key, direct: true);
        using var response = await ReleaseAndJoinAsync(pending, entered, release, boundary == Deadline);
        probe.RequireContextDisposed();
        await ApplicationHttpTests.ProblemAsync(response, boundary == Authority ? HttpStatusCode.Unauthorized : HttpStatusCode.ServiceUnavailable);
        Assert.Null(response.Headers.ETag);
        Assert.Null(response.Headers.Location);
        Assert.Equal(1, probe.Commits);
        Assert.Equal(1, probe.Disposals);
        await RequireDurableAsync(test, id, version, key, boundary != Authority);
    }

    /// <summary>Always releases and joins the actual request, retaining observation and transport failures before fixture teardown.</summary>
    internal static async Task<HttpResponseMessage> ReleaseAndJoinAsync(Task<HttpResponseMessage> pending, TaskCompletionSource entered, TaskCompletionSource release, bool elapsed)
    {
        List<Exception> failures = [];
        try
        {
            await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(WatchdogSeconds), TestContext.Current.CancellationToken);
            Assert.True(entered.Task.IsCompleted, CleanupNotReached);
            Assert.False(pending.IsCompleted);
            if (elapsed)
            {
                await Task.Delay(BeyondBudgetMilliseconds, TestContext.Current.CancellationToken);
                Assert.False(pending.IsCompleted);
            }
        }
        catch (Exception error) { failures.Add(error); }
        finally { release.TrySetResult(); }
        HttpResponseMessage? response = null;
        try
        {
            response = await pending;
        }
        catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0)
        {
            response?.Dispose();
            throw new AggregateException(failures);
        }
        return response!;
    }

    /// <summary>Moves authority only at actual provider transaction cleanup, or holds that cleanup across the unchanged real request timer.</summary>
    private static async Task ConfigureAsync(ApplicationHttpFixture test, GrantPublicationProbe probe, string boundary, TaskCompletionSource entered, TaskCompletionSource release)
    {
        var expiry = await ApplicationHttpUpdateEvidence.NativeDeadlineAsync(test);
        if (boundary == Authority)
        {
            test.Environment.Issuer.Account.Journey.Clock.Now = expiry.AddSeconds(-1);
        }
        probe.AfterDisposal = () =>
        {
            entered.SetResult();
            release.Task.WaitAsync(TimeSpan.FromSeconds(WatchdogSeconds), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            if (boundary == Cleanup)
            {
                throw new InvalidOperationException(CleanupFailure);
            }
            if (boundary == Authority)
            {
                test.Environment.Issuer.Account.Journey.Clock.Now = expiry;
            }
        };
    }

    /// <summary>Reconciles one actual committed approval and optionally recovers it with an explicit new request, preserving every original native/job/audit row.</summary>
    private static async Task RequireDurableAsync(ApplicationHttpFixture test, string id, string version, string key, bool recover)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await database.Set<ApplicationGrantJob>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var history = await GrantAuditWitness.ReadAsync(database, row.Id, TestContext.Current.CancellationToken);
        history.Require();
        Assert.Single(history.Approvals);
        Assert.Single(history.Audits);
        Assert.Equal(GrantJobState.Queued, row.State);
        Assert.Equal(0, row.AuthorizationCount);
        Assert.Equal(0, row.TokenCount);
        Assert.Equal(Guid.Parse(key), history.Approvals[0].RequestKey);
        if (!recover)
        {
            return;
        }
        var before = await SnapshotAsync(database, id);
        using var response = await test.GrantAsync(id, Body, version, key, direct: true);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = (await response.Content.ReadFromJsonAsync<GrantAcceptance>(TestContext.Current.CancellationToken))!;
        Assert.Equal(row.Id.ToString(GuidFormat), accepted.Job.Id);
        Assert.Equal(key, accepted.RequestKey);
        Assert.Equal(before, await SnapshotAsync(database, id));
    }
}
