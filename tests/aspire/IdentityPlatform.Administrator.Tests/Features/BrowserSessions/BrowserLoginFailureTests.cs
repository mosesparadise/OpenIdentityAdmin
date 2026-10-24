using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Client;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises genuine reservation/native commits while denying publication on uncertain acknowledgement or lost capture.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLoginFailureTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly bool[] Variants = [false, true];
    /// <summary>An actual committed reservation survives acknowledgement loss but publishes no native request or resumable retry.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ReservationCommitAcknowledgementLoss_When_ResumeRuns_Then_SpentStatePublishesNothing(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var intent = await scenario.StartAsync(await scenario.EntryAsync());
        var completion = await scenario.CompleteAsync(intent);
        var loss = new BrowserCommitLoss();
        await using var peer = await scenario.PeerAsync(services => services.AddDbContext<AdminSessionDbContext>((_, options) => options.AddInterceptors(loss)));
        using var browser = peer.Browser(scenario.Cookies);
        using var response = await scenario.ResumeAsync(completion, browser);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, loss.Commits);
        await RequireSpentAsync(scenario, completion, peer, expectedTokens: 0);
    }

    /// <summary>Removing the product capture handler cannot silently downgrade a reserved product login to an ordinary B2 challenge.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_MissingProductCapture_When_ResumeRuns_Then_NoUnboundNativeStateOrParIsCreated(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var intent = await scenario.StartAsync(await scenario.EntryAsync());
        var completion = await scenario.CompleteAsync(intent);
        await using var peer = await scenario.PeerAsync(services => services.Configure<OpenIddictClientOptions>(options =>
            options.Handlers.RemoveAll(handler => handler.ServiceDescriptor.ServiceType == typeof(BrowserLoginChallenge))));
        using var browser = peer.Browser(scenario.Cookies);
        using var response = await scenario.ResumeAsync(completion, browser);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        await RequireSpentAsync(scenario, completion, peer, expectedTokens: 0);
    }

    /// <summary>A genuinely committed native state cannot escape on child acknowledgement or cleanup failure, and its browser intent stays spent.</summary>
    [Theory, MemberData(nameof(NativeFailures))]
    public async Task Given_NativeStateCompletionFailure_When_ResumeRuns_Then_NoCookieRedirectOrAutomaticRetryEscapes(string provider, bool cleanup)
    {
        var fault = new NativeClientCreationFault(cleanup);
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, fault.Configure);
        var intent = await scenario.StartAsync(await scenario.EntryAsync());
        var completion = await scenario.CompleteAsync(intent);
        fault.Armed = true;
        using var response = await scenario.ResumeAsync(completion);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, fault.Faults);
        Assert.Equal(1, fault.Commits);
        Assert.Equal(1, fault.Disposals);
        await RequireSpentAsync(scenario, completion, scenario.Host, expectedTokens: 1);
    }

    /// <summary>The real issuer rejects a substituted completion in PAR; the product retains spent local state and never retries native transport.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ValidIntentWithWrongCompletion_When_NativeParRejects_Then_SpentReservationPublishesNoRedirectOrCookie(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var intent = await scenario.StartAsync(await scenario.EntryAsync());
        var genuine = await scenario.CompleteAsync(intent);
        using var response = await scenario.ResumeAsync(BrowserLoginSelector.CreateSelector());
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, scenario.Host.Bridge.ParSends);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
        Assert.Equal(1, await scenario.ReadAsync(db => db.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync(TestContext.Current.CancellationToken)));
        var row = await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(BrowserIntentState.Reserved, row.State);
        var before = await scenario.SnapshotAsync();
        using var replay = await scenario.ResumeAsync(genuine);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(1, scenario.Host.Bridge.ParSends);
    }

    /// <summary>Checks durable spent state through a fresh root and proves replay changes no local state or transport counts.</summary>
    private static async Task RequireSpentAsync(BrowserLoginFixture scenario, string completion, BrowserLoginHost challenged, int expectedTokens)
    {
        var intent = await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(BrowserIntentState.Reserved, intent.State);
        Assert.NotNull(intent.NativeStateDigest);
        Assert.Equal(expectedTokens, await scenario.ReadAsync(db => db.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(0, challenged.Bridge.ParSends);
        Assert.Equal(0, challenged.Bridge.TokenSends);
        var before = await scenario.SnapshotAsync();
        using var replay = await scenario.ResumeAsync(completion);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
    }

    /// <summary>Discovers each actual native child completion failure on both providers.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> NativeFailures => from provider in ProviderNames
                                                                             from cleanup in Variants
                                                                             select new TheoryDataRow<string, bool>(provider, cleanup).WithTrait(ProviderTrait, provider);
}
