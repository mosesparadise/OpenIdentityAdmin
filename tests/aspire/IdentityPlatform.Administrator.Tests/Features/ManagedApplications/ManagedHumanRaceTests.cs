using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Uses actual database wait observations before releasing competing native human requests.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedHumanRaceTests(BootstrapFixture fixture)
{
    private const string GovernanceWait = "%AdministratorGovernance%";
    private const int Competitors = 2;

    /// <summary>Two real HTTP submissions wait behind a real governance lock; only one may consume MFA or consent and advance the exact revision.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_SameManagedProof_When_TwoActualRequestsWaitThenRace_Then_OnlyOnePhaseTransitionCommits(string provider, bool consent)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var form = await MfaAsync(test);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(test.Http.Environment.Issuer.Account.Journey);
        if (consent)
        {
            using var factor = await ManagedHumanHttp.MfaAsync(test, form, code);
            await ManagedHumanHttp.EntryAsync(factor);
            form = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, factor.Headers.Location!);
        }
        var before = Assert.Single((await test.SnapshotAsync()).Interactions);
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new CeremonyLocks(database).AcquireAsync(new(true, false, [], []), TestContext.Current.CancellationToken);
        var blocker = await DatabaseWaiters.IdentifyAsync(database, TestContext.Current.CancellationToken);
        var requests = Enumerable.Range(0, Competitors).Select(_ => consent ? ManagedHumanHttp.DecideAsync(test, form) : ManagedHumanHttp.MfaAsync(test, form, code)).ToArray();
        var responses = await ManagedCacheRaceTests.ReleaseAndJoinAsync(transaction,
            () => DatabaseWaiters.ObserveAsync(test.Http.Environment.Issuer.Account.Journey.Lease, blocker, GovernanceWait, Competitors), requests);
        using var first = responses[0];
        using var second = responses[1];
        Assert.Equal(new[] { HttpStatusCode.Redirect, HttpStatusCode.ServiceUnavailable }, responses.Select(value => value.StatusCode).Order());
        var after = Assert.Single((await test.SnapshotAsync()).Interactions);
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(consent ? ManagedLoginState.Approved : ManagedLoginState.PendingConsent, after.State);
    }

    /// <summary>An account generation changed while the actual request is waiting must be read after lock acquisition and reject without consuming OTP.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ManagedMfaWaitingOnRealLock_When_AccountGenerationChanges_Then_CurrentAuthorityRejectsProof(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var form = await MfaAsync(test);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(test.Http.Environment.Issuer.Account.Journey);
        var before = Assert.Single((await test.SnapshotAsync()).Interactions);
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new CeremonyLocks(database).AcquireAsync(new(true, false, [], []), TestContext.Current.CancellationToken);
        var blocker = await DatabaseWaiters.IdentifyAsync(database, TestContext.Current.CancellationToken);
        var pending = ManagedHumanHttp.MfaAsync(test, form, code);
        var responses = await ManagedCacheRaceTests.ReleaseAndJoinAsync(transaction, async () =>
        {
            await DatabaseWaiters.ObserveAsync(test.Http.Environment.Issuer.Account.Journey.Lease, blocker, GovernanceWait, 1);
            Assert.Equal(1, await database.Set<AdministratorAuthentication>().ExecuteUpdateAsync(setter => setter
                .SetProperty(row => row.Generation, row => row.Generation + 1), TestContext.Current.CancellationToken));
        }, pending);
        using var denied = responses[0];
        await ManagedCacheTestContext.UnavailableAsync(denied);
        var after = Assert.Single((await test.SnapshotAsync()).Interactions);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(ManagedLoginState.PendingMfa, after.State);
        Assert.Null(after.MfaVerifiedAt);
        Assert.Equal(1, await database.Set<AdministratorAuthentication>().ExecuteUpdateAsync(setter => setter
            .SetProperty(row => row.Generation, row => row.Generation - 1), TestContext.Current.CancellationToken));
        using var recovered = await ManagedHumanHttp.MfaAsync(test, form, code);
        await ManagedHumanHttp.EntryAsync(recovered);
    }

    /// <summary>Obtains a genuine managed password proof before admitting race participants.</summary>
    private static async Task<Dictionary<string, string>> MfaAsync(ManagedCacheTestContext test)
    {
        var entry = await ManagedHumanHttp.StartAsync(test);
        using var password = await ManagedHumanHttp.PasswordAsync(test, entry);
        await ManagedHumanHttp.EntryAsync(password);
        return await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, password.Headers.Location!);
    }
}
