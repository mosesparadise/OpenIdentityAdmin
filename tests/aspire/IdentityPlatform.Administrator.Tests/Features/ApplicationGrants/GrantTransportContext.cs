using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.ApplicationGrants.GrantBoundaryEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Owns one genuine authenticated HTTPS journey and an exact grant operation with independently observable durable state.</summary>
internal sealed class GrantTransportContext(ApplicationHttpFixture test, GrantTransportProbe probe, string shape, string id, string version) : IAsyncDisposable
{
    internal const string Create = "create";
    internal const string Continue = "continue";
    internal const string Exact = "exact";
    internal const string Recent = "recent";
    private const string ContinueSuffix = "/continue";
    private const string Reason = "security";
    private const int ContinuedApprovals = 2;
    private const int ExtendedSeconds = 60;
    private const string SessionPath = "/bff/session";
    private const string Authenticated = "authenticated";
    private const string IdentityHeader = "X-Identity";
    internal ApplicationHttpFixture Test => test;
    internal GrantTransportProbe Probe => probe;
    internal bool Mutation => shape is Create or Continue;
    internal string? JobId
    {
        get; private set;
    }
    private string csrf = string.Empty;
    private string key = Guid.NewGuid().ToString(GuidFormat);
    private string body = Body;
    private string Path => ApplicationHttpFixture.Collection + Separator + id + Suffix +
        (shape == Recent || shape == Create ? string.Empty : Separator + JobId) + (shape == Continue ? ContinueSuffix : string.Empty);

    /// <summary>Prepares real managed target and, for continuation, a genuinely authority-stopped job without manufacturing approvals.</summary>
    internal static async Task<GrantTransportContext> CreateAsync(BootstrapFixture fixture, string provider, string shape)
    {
        var probe = new GrantTransportProbe();
        var test = await ApplicationHttpFixture.CreateAsync(fixture, provider, configureApi: services => services.AddSingleton<IStartupFilter>(probe));
        try
        {
            var (id, version) = await GrantHttpTests.DisabledAsync(test);
            var context = new GrantTransportContext(test, probe, shape, id, version);
            await context.InitializeAsync();
            return context;
        }
        catch { await test.DisposeAsync(); throw; }
    }

    /// <summary>Stages only prerequisite work, retaining the tested create/continue as a fresh command until the caller sends it.</summary>
    private async Task InitializeAsync()
    {
        if (shape != Create)
        {
            using var response = await test.GrantAsync(id, Body, version, key);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            JobId = (await response.Content.ReadFromJsonAsync<GrantAcceptance>(TestContext.Current.CancellationToken))!.Job.Id;
            if (shape == Continue)
            {
                await GrantHttpRecoveryTests.StopAndRestoreAsync(test, JobId);
                await using var scope = test.Scope();
                var job = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationGrantJob>().AsNoTracking()
                    .SingleAsync(TestContext.Current.CancellationToken);
                Assert.Equal(GrantJobState.Stopped, job.State);
                body = JsonSerializer.Serialize(new GrantRevocation(Reason, true, ApplicationVersion.Create(JobId, job.Revision)), JsonSerializerOptions.Web);
                key = Guid.NewGuid().ToString(GuidFormat);
            }
        }
        csrf = await test.TokenAsync();
        await probe.BindAsync(test);
        probe.Path = Path.Replace(ApplicationHttpFixture.Collection, ApplicationHttpFixture.ApiCollection, StringComparison.Ordinal);
        probe.Body = Mutation ? body : null;
        probe.Key = Mutation ? key : null;
        probe.Version = Mutation ? version : null;
        test.Browser.DefaultRequestHeaders.Add(IdentityHeader, GrantTransportProbe.Canary);
    }

    /// <summary>Sends an explicit browser operation; every test request preserves the same exact immutable key and payload.</summary>
    internal Task<HttpResponseMessage> SendAsync() => Mutation
        ? test.GrantAsync(id, body, version, key, jobId: shape == Continue ? JobId : null, csrf: csrf)
        : test.Browser.GetAsync(Path, TestContext.Current.CancellationToken);

    /// <summary>Uses a fresh independent database scope to compare all native, target, grant and audit fields.</summary>
    internal async Task<string> SnapshotAsync()
    {
        await using var scope = test.Scope();
        return await GrantBoundaryEvidence.SnapshotAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>(), id);
    }

    /// <summary>Changes only the actual durable browser generation or its short expiry, leaving issuer authority and API grants live.</summary>
    internal async Task ChangeBrowserAsync(bool generation)
    {
        await using var scope = test.Environment.Client.Services.CreateAsyncScope();
        var rows = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<BrowserContext>();
        var affected = generation
            ? await rows.ExecuteUpdateAsync(update => update.SetProperty(row => row.Generation, row => row.Generation + 1), TestContext.Current.CancellationToken)
            : await rows.ExecuteUpdateAsync(update => update.SetProperty(row => row.ExpiresAt, test.Now.AddSeconds(1).UtcTicks), TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
    }

    /// <summary>Extends only the persisted browser expiry while the original request is held, then reaches its original short deadline.</summary>
    internal async Task ExtendAndExpireOriginalAsync()
    {
        await using var scope = test.Environment.Client.Services.CreateAsyncScope();
        var rows = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<BrowserContext>();
        Assert.Equal(1, await rows.ExecuteUpdateAsync(update => update.SetProperty(row => row.ExpiresAt, test.Now.AddSeconds(ExtendedSeconds).UtcTicks),
            TestContext.Current.CancellationToken));
        test.Environment.Issuer.Account.Journey.Clock.Now = test.Now.AddSeconds(1);
        using var current = await test.Browser.GetAsync(SessionPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        using var status = JsonDocument.Parse(await current.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(status.RootElement.GetProperty(Authenticated).GetBoolean());
    }

    /// <summary>Proves truthful durable acceptance despite withheld browser output; explicit authorized API replay recovers the same command once.</summary>
    internal async Task RequireRecoveryAsync()
    {
        var before = await SnapshotAsync();
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var job = await database.Set<ApplicationGrantJob>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var history = await GrantAuditWitness.ReadAsync(database, job.Id, TestContext.Current.CancellationToken);
        history.Require();
        Assert.Equal(shape == Continue ? ContinuedApprovals : 1, history.Approvals.Length);
        Assert.Equal(GrantJobState.Queued, job.State);
        if (Mutation)
        {
            probe.Path = string.Empty;
            using var recovered = await test.GrantAsync(id, body, version, key, direct: true, jobId: shape == Continue ? JobId : null);
            Assert.Equal(HttpStatusCode.Accepted, recovered.StatusCode);
            var accepted = (await recovered.Content.ReadFromJsonAsync<GrantAcceptance>(TestContext.Current.CancellationToken))!;
            Assert.Equal(job.Id.ToString(GuidFormat), accepted.Job.Id);
            Assert.Equal(key, accepted.RequestKey);
        }
        Assert.Equal(before, await SnapshotAsync());
    }

    /// <summary>Joins all actual network and database owners after the response probe's held work has been released.</summary>
    public ValueTask DisposeAsync() => test.DisposeAsync();
}
