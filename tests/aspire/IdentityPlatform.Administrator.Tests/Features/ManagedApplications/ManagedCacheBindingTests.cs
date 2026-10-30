using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Checks current restored request/browser/revision and original time across genuine native HTTPS requests.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedCacheBindingTests(BootstrapFixture fixture)
{
    private const int ObservationAdvanceSeconds = 100;
    private const string ForeignCallback = "https://foreign.example.test/callback";
    private const string ForeignState = "foreign-state";
    private const string ForeignScope = "foreign.scope";
    private const string ForeignChallenge = "foreign-challenge";

    /// <summary>The actual native restore protects original intent; a browser without the separate cookie never reaches interaction observation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_BoundCachedRequest_When_OuterDemandOrBrowserChanges_Then_OnlyOriginalBrowserObservesOriginalIntent(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        using var initial = await test.SendAsync(ManagedCacheTestContext.Initial());
        var resume = await test.ResumeAsync(initial);
        var before = Assert.Single((await test.SnapshotAsync()).Interactions);
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = test.Http.Now.AddSeconds(1);
        using (var foreign = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }))
        using (var denied = await foreign.GetAsync(initial.Headers.Location, TestContext.Current.CancellationToken))
        {
            await ManagedCacheTestContext.UnavailableAsync(denied);
        }
        Assert.Equal(before.ObservedAt, Assert.Single((await test.SnapshotAsync()).Interactions).ObservedAt);
        resume[OpenIddictConstants.Parameters.RedirectUri] = ForeignCallback;
        resume[OpenIddictConstants.Parameters.State] = ForeignState;
        resume[OpenIddictConstants.Parameters.Scope] = ForeignScope;
        resume[OpenIddictConstants.Parameters.CodeChallenge] = ForeignChallenge;
        using var pending = await test.SendAsync(resume);
        await ManagedHumanHttp.EntryAsync(pending);
        var restored = Assert.Single((await test.SnapshotAsync()).Interactions);
        Assert.Equal(test.Http.Now.ToUnixTimeSeconds(), restored.ObservedAt);
        Assert.Equal(before.DemandDigest, restored.DemandDigest);
        Assert.Equal(before.ExpiresAt, restored.ExpiresAt);
        Assert.Equal(before.RequestId, restored.RequestId);
    }

    /// <summary>Two actual browser proofs each bind their own native request; a valid foreign cookie cannot resume the first interaction.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TwoRealManagedBrowsers_When_FirstCacheIsSentWithSecondProof_Then_FirstInteractionRemainsUnobserved(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        using var first = await test.SendAsync(ManagedCacheTestContext.Initial());
        _ = await test.ResumeAsync(first);
        var original = Assert.Single((await test.SnapshotAsync()).Interactions);
        using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
        {
            BaseAddress = test.Http.Environment.Profile.Issuer
        };
        using var second = await browser.GetAsync(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(NativeCacheProfile.Authorize,
            ManagedCacheTestContext.Initial().Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))), TestContext.Current.CancellationToken);
        _ = await test.ResumeAsync(second);
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = test.Http.Now.AddSeconds(1);
        using var own = await browser.GetAsync(second.Headers.Location, TestContext.Current.CancellationToken);
        await ManagedHumanHttp.EntryAsync(own);
        var positive = await test.SnapshotAsync();
        var secondRow = Assert.Single(positive.Interactions, row => row.RequestId != original.RequestId);
        Assert.NotEqual(original.BrowserDigest, secondRow.BrowserDigest);
        Assert.Equal(test.Http.Now.ToUnixTimeSeconds(), secondRow.ObservedAt);
        using var substituted = await browser.GetAsync(first.Headers.Location, TestContext.Current.CancellationToken);
        await ManagedCacheTestContext.UnavailableAsync(substituted);
        Assert.Equal(original.ObservedAt, Assert.Single((await test.SnapshotAsync()).Interactions, row => row.RequestId == original.RequestId).ObservedAt);
    }

    /// <summary>Aggregate changes invalidate a restored request, while expiry equality prevents any new cache creation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CachedApplication_When_RevisionChangesOrCredentialExpires_Then_NoStaleDecisionOrNewNativeArtifactIsAdmitted(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        using var initial = await test.SendAsync(ManagedCacheTestContext.Initial());
        var resume = await test.ResumeAsync(initial);
        var before = Assert.Single((await test.SnapshotAsync()).Interactions);
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = test.Http.Now.AddSeconds(1);
        await using (var scope = test.Http.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await database.Set<ApplicationPolicy>().Where(row => row.ApplicationId == test.Id)
                .ExecuteUpdateAsync(setter => setter.SetProperty(row => row.Revision, row => row.Revision + 1), TestContext.Current.CancellationToken);
        }
        using var stale = await test.SendAsync(resume);
        await ManagedCacheTestContext.UnavailableAsync(stale);
        Assert.Equal(before.ObservedAt, Assert.Single((await test.SnapshotAsync()).Interactions).ObservedAt);
        await using (var scope = test.Http.Scope())
        {
            var policy = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationPolicy>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            test.Http.Environment.Issuer.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(policy.CredentialExpiresAt);
        }
        using var expired = await test.SendAsync(ManagedCacheTestContext.Initial());
        await ManagedCacheTestContext.UnavailableAsync(expired);
        Assert.Single((await test.SnapshotAsync()).Tokens);
    }

    /// <summary>Committed cache observations survive across requests and reject rollback beyond the shared thirty-second tolerance.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CommittedManagedObservation_When_NextRequestClockRollsBack_Then_DurableWatermarkRejectsNewCache(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var original = test.Http.Now;
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = original.AddSeconds(ObservationAdvanceSeconds);
        using var first = await test.SendAsync(ManagedCacheTestContext.Initial());
        _ = await test.ResumeAsync(first);
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = original;
        using var rollback = await test.SendAsync(ManagedCacheTestContext.Initial());
        await ManagedCacheTestContext.UnavailableAsync(rollback);
        Assert.Single((await test.SnapshotAsync()).Tokens);
        await using var scope = test.Http.Scope();
        var policy = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationPolicy>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(original.AddSeconds(ObservationAdvanceSeconds).ToUnixTimeSeconds(), policy.ObservedAt);
        Assert.Equal(1, policy.Revision);
    }
}
