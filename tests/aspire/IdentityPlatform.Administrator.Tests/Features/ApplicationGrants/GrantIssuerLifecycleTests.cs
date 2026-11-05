using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.ManagedApplications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Separates causal committed-entry checks from genuine same-issuer cross-host lifecycle and cryptographic controls.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantIssuerLifecycleTests(BootstrapFixture fixture)
{
    private const string OtherClient = "unaffected-revocation-client";
    private const string InvalidGrant = "invalid_grant";
    private const long InitialRevision = 1;
    private const long DisabledRevision = 2;

    /// <summary>A real batch becomes visible on both independent issuer roots within five seconds; unrelated and freshly approved exchanges still work.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineCodesAndTwoIssuerHosts_When_TargetBatchCommits_Then_EntryDenialIsTimelyAndPeerJourneysRemainIsolated(string provider)
    {
        var commit = new GrantBatchCommitObservation();
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider,
            configureApi: services => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(commit)));
        var target = new ManagedTokenFlow(test);
        await target.ApproveAsync();
        var oldCode = await target.CodeAsync();
        var other = await OtherAsync(test);
        var control = new ManagedTokenFlow(other);
        await control.ApproveAsync();
        var controlCode = await control.CodeAsync();
        using var endpoint = new BrowserHttpsEndpoint();
        await using var peer = await BrowserIssuerHost.CreateHttpsAsync(test.Http.Environment.Issuer.Account.Journey, provider,
            test.Http.Environment.Profile.Registration, test.Http.Environment.Profile.ClientUri, endpoint, managed: true);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = endpoint.Address };
        client.DefaultRequestHeaders.Host = test.Http.Environment.Profile.Issuer.Authority;
        var accepted = await RevokeAndObserveAsync(test, other, peer.Services, commit);
        using var denied = await target.ExchangeAsync(oldCode, client: client);
        await ManagedTokenEvidence.ErrorAsync(denied, HttpStatusCode.BadRequest, InvalidGrant);
        await ExchangeAsync(other, control, controlCode, client);
        Assert.True(await GrantIssuerRevocation.BatchAsync(test, accepted.Id));
        await RequireCompleteAsync(test, accepted.Id);
        await ManagedLifecycleJourneyTests.TransitionAsync(test, true, DisabledRevision);
        var fresh = new ManagedTokenFlow(test);
        await fresh.ApproveAsync();
        await ExchangeAsync(test, fresh, await fresh.CodeAsync(), client);
    }

    /// <summary>Creates a second real managed registration, borrowing the same fixture lifetime without sharing grant ownership.</summary>
    private static async Task<ManagedCacheTestContext> OtherAsync(ManagedCacheTestContext test)
    {
        using var response = await test.Http.CreateAsync(ApplicationHttpTests.Input(test.Http.Now, OtherClient));
        return new(test.Http, await ApplicationHttpTests.CreatedAsync(response));
    }

    /// <summary>Warms independent native roots, commits the target batch, then bounds all target and unaffected entry checks from that exact commit.</summary>
    private static async Task<GrantJobView> RevokeAndObserveAsync(ManagedCacheTestContext test, ManagedCacheTestContext other,
        IServiceProvider peer, GrantBatchCommitObservation commit)
    {
        var targetEntry = await GrantIssuerEntry.ReadAsync(test);
        var controlEntry = await GrantIssuerEntry.ReadAsync(other);
        var clock = test.Http.Environment.Issuer.Account.Journey.Clock;
        var roots = new[] { test.Http.Environment.IssuerServices, peer };
        foreach (var root in roots)
        {
            await targetEntry.RequireAsync(root, clock, true);
            await controlEntry.RequireAsync(root, clock, true);
        }
        var unchanged = await ManagedJourneyEvidence.RegistrationAsync(other);
        await ManagedLifecycleJourneyTests.TransitionAsync(test, false, InitialRevision);
        var accepted = await GrantIssuerRevocation.AcceptAsync(test);
        commit.Job = Guid.Parse(accepted.Id);
        Assert.True(await GrantIssuerRevocation.BatchAsync(test, accepted.Id));
        foreach (var root in roots)
        {
            await targetEntry.RequireAsync(root, clock, false);
            await controlEntry.RequireAsync(root, clock, true);
        }
        commit.RequireVisible();
        Assert.Equal(unchanged, await ManagedJourneyEvidence.RegistrationAsync(other));
        return accepted;
    }

    /// <summary>Proves the peer's actual Basic/S256 exchange, signature, issuer, nonce, lifetime and native row bindings for the original flow.</summary>
    private static async Task ExchangeAsync(ManagedCacheTestContext test, ManagedTokenFlow flow, string code, HttpClient peer)
    {
        using var response = await flow.ExchangeAsync(code, client: peer);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        ManagedHumanHttp.NoAuthorityCookie(response);
        using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var interaction = Assert.Single((await test.SnapshotAsync(applicationOnly: true)).Interactions, row => row.RequestId == flow.RequestId);
        await ManagedTokenOracle.ValidateAsync(test, flow, body.RootElement, interaction.UserId!, applicationOnly: true, multipleFlows: true);
    }

    /// <summary>Requires a successful zero-remaining reconciliation with immutable acceptance and exact one-code/one-authorization progress before enabling.</summary>
    private static async Task RequireCompleteAsync(ManagedCacheTestContext test, string job)
    {
        await using var scope = test.Http.Scope();
        var history = await GrantAuditWitness.ReadAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>(), Guid.Parse(job), TestContext.Current.CancellationToken);
        history.Require();
        Assert.Equal(GrantJobState.Succeeded, history.Job.State);
        Assert.Equal(1, history.Job.AuthorizationCount);
        Assert.Equal(1, history.Job.TokenCount);
        Assert.Single(history.Approvals);
    }
}
