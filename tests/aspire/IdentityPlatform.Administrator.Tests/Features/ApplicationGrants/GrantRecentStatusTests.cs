using System.Data.SqlTypes;
using System.Net;
using System.Net.Http.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.ApplicationGrants.GrantBoundaryEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Verifies bounded real HTTP status reads against independently ordered persisted jobs, including equal timestamps and a newer foreign target.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantRecentStatusTests(BootstrapFixture fixture)
{
    private const int HistorySize = 12;
    private const int EqualTimeJobs = 6;
    private const int ExpectedLimit = 10;
    private const string PeerClient = "grant-status-peer";
    private const string SqlServer = "SqlServer";
    private const string Succeeded = "succeeded";
    private const string None = "none";
    private const string Zero = "0";
    private const int ReadRepetitions = 2;

    /// <summary>Twelve genuine completed jobs yield exactly the latest ten; matching timestamp ties are stable and foreign jobs never enter the page or exact route.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_MoreThanTenJobsAndNewerPeer_When_StatusIsReadOverHttps_Then_OnlyExactTargetNewestTenArePublished(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        var (id, version) = await GrantHttpTests.DisabledAsync(test);
        using var empty = await test.DirectGetAsync(ApplicationHttpFixture.ApiCollection + Separator + id + Suffix);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Empty((await empty.Content.ReadFromJsonAsync<GrantJobPage>(TestContext.Current.CancellationToken))!.Items);
        for (var index = 0; index < HistorySize; index++)
        {
            if (index >= EqualTimeJobs)
            {
                test.Environment.Issuer.Account.Journey.Clock.Now = test.Now.AddSeconds(1);
            }
            await CompleteAsync(test, id, version);
        }
        var peer = await PeerAsync(test);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = await database.Set<ApplicationGrantJob>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HistorySize + 1, rows.Length);
        var comparer = Comparer<Guid>.Create((left, right) => provider == SqlServer ? new SqlGuid(left).CompareTo(new SqlGuid(right)) : left.CompareTo(right));
        var expected = rows.Where(row => row.ApplicationId == id).OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id, comparer).Take(ExpectedLimit).ToArray();
        Assert.Equal(EqualTimeJobs, rows.Count(row => row.ApplicationId == id && row.CreatedAt == rows.Where(value => value.ApplicationId == id).Min(value => value.CreatedAt)));
        var before = await SnapshotAsync(database, id);
        await RequireReadsAsync(test, id, peer, expected);
        Assert.Equal(before, await SnapshotAsync(database, id));
    }

    /// <summary>Admits through real bearer validation then performs an actual empty-set Worker reconciliation, retaining durable approval and audit.</summary>
    private static async Task<string> CompleteAsync(ApplicationHttpFixture test, string id, string version)
    {
        using var response = await test.GrantAsync(id, Body, version, Guid.NewGuid().ToString(GuidFormat), direct: true);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = (await response.Content.ReadFromJsonAsync<GrantAcceptance>(TestContext.Current.CancellationToken))!;
        Assert.True(await GrantBatchOperation.ExecuteAsync(test.Environment.Api!.Services.GetRequiredService<IServiceScopeFactory>(), Guid.Parse(accepted.Job.Id),
            test.Environment.Issuer.Account.Journey.Clock, TestContext.Current.CancellationToken));
        return accepted.Job.Id;
    }

    /// <summary>Creates a distinct managed target and a newer committed job to expose filtering after limiting or missing exact target binding.</summary>
    private static async Task<string> PeerAsync(ApplicationHttpFixture test)
    {
        test.Environment.Issuer.Account.Journey.Clock.Now = test.Now.AddSeconds(1);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now, PeerClient));
        var peer = await ApplicationHttpTests.CreatedAsync(create);
        using var disabled = await test.UpdateAsync(peer.Application.Id, Body, create.Headers.ETag!.Tag, enabled: false);
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        return await CompleteAsync(test, peer.Application.Id, disabled.Headers.ETag!.Tag);
    }

    /// <summary>Repeats collection reads on both surfaces, checks all sanitized fields and exact ETag, and rejects a real peer job under the target route.</summary>
    private static async Task RequireReadsAsync(ApplicationHttpFixture test, string id, string peer, ApplicationGrantJob[] expected)
    {
        foreach (var direct in new[] { false, true })
        {
            var path = (direct ? ApplicationHttpFixture.ApiCollection : ApplicationHttpFixture.Collection) + Separator + id + Suffix;
            for (var repeat = 0; repeat < ReadRepetitions; repeat++)
            {
                using var response = await ReadAsync(test, path, direct);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(response.Headers.CacheControl!.NoStore);
                var page = (await response.Content.ReadFromJsonAsync<GrantJobPage>(TestContext.Current.CancellationToken))!;
                Assert.Equal(ExpectedLimit, page.Items.Count);
                Assert.Equal(expected.Select(row => row.Id.ToString(GuidFormat)), page.Items.Select(row => row.Id));
                for (var index = 0; index < expected.Length; index++)
                {
                    RequireView(expected[index], page.Items[index], page.ExpiresAt);
                }
            }
            using var exact = await ReadAsync(test, path + Separator + expected[0].Id.ToString(GuidFormat), direct);
            Assert.Equal(HttpStatusCode.OK, exact.StatusCode);
            var view = (await exact.Content.ReadFromJsonAsync<GrantJobView>(TestContext.Current.CancellationToken))!;
            RequireView(expected[0], view, view.ExpiresAt);
            Assert.Equal(view.Version, exact.Headers.ETag!.Tag);
            using var foreign = await ReadAsync(test, path + Separator + peer, direct);
            await ApplicationHttpTests.ProblemAsync(foreign, HttpStatusCode.NotFound);
        }
    }

    /// <summary>Uses real cookie or bearer transport without replacing endpoint authentication or response parsing.</summary>
    private static Task<HttpResponseMessage> ReadAsync(ApplicationHttpFixture test, string path, bool direct) => direct
        ? test.DirectGetAsync(path) : test.Browser.GetAsync(path, TestContext.Current.CancellationToken);

    /// <summary>Independently checks completed empty-job semantics, canonical values, persisted chronology and bounded response authority.</summary>
    private static void RequireView(ApplicationGrantJob row, GrantJobView view, DateTimeOffset expiry)
    {
        Assert.Equal(row.Id.ToString(GuidFormat), view.Id);
        Assert.Equal(row.ApplicationId, view.ApplicationId);
        Assert.Equal(ApplicationVersion.Create(view.Id, row.Revision), view.Version);
        Assert.Equal(Succeeded, view.State);
        Assert.Equal(None, view.Failure);
        Assert.Equal(Zero, view.AuthorizationCount);
        Assert.Equal(Zero, view.TokenCount);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(row.CreatedAt), view.CreatedAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(row.ObservedAt), view.UpdatedAt);
        Assert.Equal(expiry, view.ExpiresAt);
    }
}
