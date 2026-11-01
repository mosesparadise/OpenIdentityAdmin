using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Uses provider-observed lock waits to test current expiry and concurrent native redemption across actual issuer roots.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedTokenRaceTests(BootstrapFixture fixture)
{
    private const string InvalidGrant = "invalid_grant";
    private const string InvalidToken = "invalid_token";
    private const string GovernanceWait = "%AdministratorGovernance%";
    private const int Competitors = 2;

    /// <summary>A request really blocked on governance cannot issue after its original human deadline becomes equal to the current time.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ManagedIssuanceWaitingOnGovernance_When_OriginalDeadlineExpires_Then_NoCredentialOrRenewedDeadlineCommits(string provider, bool exchange)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = exchange ? await flow.CodeAsync() : null;
        var expires = Assert.Single((await test.SnapshotAsync()).Interactions).ExpiresAt;
        var before = await ManagedHumanEvidence.DigestAsync(test);
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new CeremonyLocks(database).AcquireAsync(new(true, false, [], []), TestContext.Current.CancellationToken);
        var blocker = await DatabaseWaiters.IdentifyAsync(database, TestContext.Current.CancellationToken);
        var pending = exchange ? flow.ExchangeAsync(code!) : test.SendAsync(flow.Resume);
        var responses = await ManagedCacheRaceTests.ReleaseAndJoinAsync(transaction, async () =>
        {
            await DatabaseWaiters.ObserveAsync(test.Http.Environment.Issuer.Account.Journey.Lease, blocker, GovernanceWait, 1);
            test.Http.Environment.Issuer.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(expires);
        }, pending);
        using var response = responses[0];
        await ManagedTokenEvidence.ErrorAsync(response, HttpStatusCode.BadRequest, exchange ? InvalidGrant : InvalidToken, local: !exchange);
        Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
    }

    /// <summary>Two real issuer roots wait on the same lock before exchanging one genuine code; at most one set of credential rows can commit.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_OneManagedCode_When_TwoIssuerRootsCompete_Then_OnlyOneIssuanceCanCommit(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = await flow.CodeAsync();
        var before = await test.SnapshotAsync();
        var authority = await ManagedHumanEvidence.AuthorityAsync(test);
        Assert.Equal(0, await ManagedTokenEvidence.ExchangeAuditsAsync(test));
        using var endpoint = new BrowserHttpsEndpoint();
        await using var peer = await BrowserIssuerHost.CreateHttpsAsync(test.Http.Environment.Issuer.Account.Journey, provider,
            test.Http.Environment.Profile.Registration, test.Http.Environment.Profile.ClientUri, endpoint, managed: true);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = endpoint.Address };
        client.DefaultRequestHeaders.Host = test.Http.Environment.Profile.Issuer.Authority;
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new CeremonyLocks(database).AcquireAsync(new(true, false, [], []), TestContext.Current.CancellationToken);
        var blocker = await DatabaseWaiters.IdentifyAsync(database, TestContext.Current.CancellationToken);
        var first = flow.ExchangeAsync(code);
        var second = flow.ExchangeAsync(code, client: client);
        var responses = await ManagedCacheRaceTests.ReleaseAndJoinAsync(transaction,
            () => DatabaseWaiters.ObserveAsync(test.Http.Environment.Issuer.Account.Journey.Lease, blocker, GovernanceWait, Competitors), first, second);
        using var primary = responses[0];
        using var secondary = responses[1];
        var after = await ManagedTokenRaceEvidence.RequireAsync(test, before, responses);
        using var replay = await flow.ExchangeAsync(code);
        await ManagedTokenEvidence.ErrorAsync(replay, HttpStatusCode.BadRequest, InvalidGrant);
        await ManagedTokenRaceEvidence.RequireFinalReplayAsync(test, after);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(test));
    }
}
