using System.Net;
using System.Net.Http.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.ApplicationGrants.GrantBoundaryEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Proves the distinct rollback-only status path joins actual cleanup and preserves original publication bounds without changing durable state.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantReadPublicationTests(BootstrapFixture fixture)
{
    private const int CleanupWatchdogSeconds = 30;
    /// <summary>Exact and collection reads cannot disclose a stale success after source expiry or the real original request timer during actual native transaction cleanup.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CurrentStatusRead_When_CleanupOutlivesOriginalAuthorityOrBudget_Then_NoMetadataIsPublishedOrMutated(string provider, bool elapsed)
    {
        using var probe = new GrantReadPublicationProbe();
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider, configureApi: probe.Configure);
        var (id, version) = await GrantHttpTests.DisabledAsync(test);
        using var admitted = await test.GrantAsync(id, Body, version, Guid.NewGuid().ToString(GuidFormat), direct: true);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        var accepted = (await admitted.Content.ReadFromJsonAsync<GrantAcceptance>(TestContext.Current.CancellationToken))!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await ConfigureAsync(test, probe, elapsed, entered, release);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var before = await SnapshotAsync(database, id);
        var path = ApplicationHttpFixture.ApiCollection + Separator + id + Suffix + (elapsed ? string.Empty : Separator + accepted.Job.Id);
        probe.Armed = true;
        var pending = test.DirectGetAsync(path);
        using var response = await GrantPublicationTests.ReleaseAndJoinAsync(pending, entered, release, elapsed);
        probe.RequireContextDisposed();
        await ApplicationHttpTests.ProblemAsync(response, elapsed ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Unauthorized);
        Assert.Null(response.Headers.ETag);
        Assert.Null(response.Headers.Location);
        Assert.DoesNotContain(accepted.Job.Id, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, probe.Reads);
        Assert.Equal(1, probe.Disposals);
        Assert.Equal(before, await SnapshotAsync(database, id));
    }

    /// <summary>Crosses only the exact direct-API native deadline, or retains a live source while the independent request timer expires.</summary>
    private static async Task ConfigureAsync(ApplicationHttpFixture test, GrantReadPublicationProbe probe, bool elapsed, TaskCompletionSource entered, TaskCompletionSource release)
    {
        var expiry = await ApplicationHttpUpdateEvidence.NativeDeadlineAsync(test);
        if (!elapsed)
        {
            test.Environment.Issuer.Account.Journey.Clock.Now = expiry.AddSeconds(-1);
        }
        probe.AfterDisposal = () =>
        {
            entered.SetResult();
            release.Task.WaitAsync(TimeSpan.FromSeconds(CleanupWatchdogSeconds), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            if (!elapsed)
            {
                test.Environment.Issuer.Account.Journey.Clock.Now = expiry;
            }
        };
    }
}
