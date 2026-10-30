using System.Net;
using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Uses actual provider wait graphs to prove cross-host admission serialization and current credential rechecks after waiting.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedCacheRaceTests(BootstrapFixture fixture)
{
    private const string GovernanceWait = "%AdministratorGovernance%";
    private const int ConcurrentRequests = 2;
    private const int LiveLimit = 128;

    /// <summary>Two independently hosted native issuers compete for the same last global slot; exactly one commits a cache.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OneGlobalSlot_When_TwoIssuerRootsWaitThenRace_Then_ExactlyOneNativeCacheCommits(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        await ManagedCacheCapacityTests.FillInteractionsAsync(test, LiveLimit - 1, false);
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
        var first = test.SendAsync(ManagedCacheTestContext.Initial());
        var second = client.GetAsync(QueryHelpers.AddQueryString(NativeCacheProfile.Authorize,
            ManagedCacheTestContext.Initial().Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))), TestContext.Current.CancellationToken);
        var responses = await ReleaseAndJoinAsync(transaction,
            () => DatabaseWaiters.ObserveAsync(test.Http.Environment.Issuer.Account.Journey.Lease, blocker, GovernanceWait, ConcurrentRequests), first, second);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        Assert.Equal(new[] { HttpStatusCode.Redirect, HttpStatusCode.ServiceUnavailable }, new[] { firstResponse.StatusCode, secondResponse.StatusCode }.Order());
        var after = await test.SnapshotAsync();
        Assert.Single(after.Tokens);
        Assert.Equal(LiveLimit, after.Interactions.Length);
        Assert.Equal(0, after.Authorizations);
    }

    /// <summary>A credential expiry committed while the actual cache child waits on governance is observed before native creation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ManagedCacheWaitingOnGovernance_When_CredentialExpiresBeforeAcquisition_Then_NoNativeOrInteractionRowCommits(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new CeremonyLocks(database).AcquireAsync(new(true, false, [], []), TestContext.Current.CancellationToken);
        var blocker = await DatabaseWaiters.IdentifyAsync(database, TestContext.Current.CancellationToken);
        var pending = test.SendAsync(ManagedCacheTestContext.Initial());
        var responses = await ReleaseAndJoinAsync(transaction, async () =>
        {
            await DatabaseWaiters.ObserveAsync(test.Http.Environment.Issuer.Account.Journey.Lease, blocker, GovernanceWait, 1);
            var expiry = test.Http.Now.ToUnixTimeSeconds() + 1;
            Assert.Equal(1, await database.Set<ApplicationPolicy>().Where(row => row.ApplicationId == test.Id)
                .ExecuteUpdateAsync(setter => setter.SetProperty(row => row.CredentialExpiresAt, expiry), TestContext.Current.CancellationToken));
            test.Http.Environment.Issuer.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(expiry);
        }, pending);
        using var response = responses[0];
        await ManagedCacheTestContext.UnavailableAsync(response);
        var after = await test.SnapshotAsync();
        Assert.Empty(after.Tokens);
        Assert.Empty(after.Interactions);
    }

    /// <summary>Releases the real lock and joins every HTTP request even when observation fails, retaining the primary failure.</summary>
    private static async Task<HttpResponseMessage[]> ReleaseAndJoinAsync(IDbContextTransaction transaction, Func<Task> observe,
        params Task<HttpResponseMessage>[] requests)
    {
        List<Exception> failures = [];
        try
        {
            await observe();
        }
        catch (Exception failure) { failures.Add(failure); }
        try
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception failure) { failures.Add(failure); }
        try
        {
            await transaction.DisposeAsync();
        }
        catch (Exception failure) { failures.Add(failure); }
        HttpResponseMessage[] responses = [];
        try
        {
            responses = await Task.WhenAll(requests);
        }
        catch (Exception failure) { failures.Add(failure); }
        if (failures.Count == 0)
        {
            return responses;
        }
        foreach (var request in requests.Where(request => request.IsCompletedSuccessfully))
        {
            request.Result.Dispose();
        }
        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        throw new AggregateException(failures);
    }

}
