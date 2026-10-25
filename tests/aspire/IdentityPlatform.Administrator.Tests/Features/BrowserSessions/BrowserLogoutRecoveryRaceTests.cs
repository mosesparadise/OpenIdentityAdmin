using System.Net;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires provider-observed serialization between acknowledgement recovery and a new explicit sign-in.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLogoutRecoveryRaceTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private static readonly bool[] Orders = [false, true];
    private const string CookieSeparator = "; ";
    private const string CookieDelimiter = ",";

    /// <summary>Recovery either observes the original tombstone or denies after replacement, without adding an audit or revoking the new intent.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_ReconciliationAndExplicitLogin_When_BrowserWriterLockContends_Then_NewIntentSurvivesBothCommitOrders(string provider, bool recoveryFirst)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        var token = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        var cookies = new CookieContainer();
        cookies.SetCookies(scenario.Profile.ClientUri, scenario.Cookies.GetCookieHeader(scenario.Profile.ClientUri).Replace(CookieSeparator, CookieDelimiter, StringComparison.Ordinal));
        using var delivered = scenario.Host.Browser(cookies);
        using var logout = await BrowserLogoutHttp.PostAsync(delivered, token);
        await BrowserLogoutHttp.CommittedAsync(logout);
        var recoveryToken = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        var loginToken = await scenario.EntryAsync(delivered);
        var recoveryProbe = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        var loginProbe = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        await using var recoveryHost = await scenario.PeerAsync(recoveryProbe.Configure);
        await using var loginHost = await scenario.PeerAsync(loginProbe.Configure);
        using var recoveryBrowser = recoveryHost.Browser(scenario.Cookies);
        using var loginBrowser = loginHost.Browser(cookies);
        var locked = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        (recoveryFirst ? recoveryProbe : loginProbe).Locked = locked;
        (recoveryFirst ? recoveryProbe : loginProbe).Release = release.Task;
        (recoveryFirst ? loginProbe : recoveryProbe).BeforeLock = waiting;
        Func<Task<HttpResponseMessage>> recover = () => BrowserLogoutHttp.PostAsync(recoveryBrowser, recoveryToken);
        Func<Task<HttpResponseMessage>> start = () => scenario.PostAsync(loginToken, loginBrowser);
        var responses = await NativeBlockedRace.InvokeAsync(recoveryFirst ? recover : start, locked.Task,
            recoveryFirst ? start : recover, waiting.Task, release, scenario.Database.ObserveAsync);
        using var first = responses[0];
        using var second = responses[1];
        Assert.Equal(HttpStatusCode.SeeOther, (recoveryFirst ? second : first).StatusCode);
        Assert.Equal(recoveryFirst ? HttpStatusCode.OK : HttpStatusCode.Conflict, (recoveryFirst ? first : second).StatusCode);
        await RequireNewIntentAsync(scenario);
    }

    /// <summary>Reads the final generation and unique logout audit independently of response arrival or cookie ordering.</summary>
    private static Task<bool> RequireNewIntentAsync(BrowserLoginFixture scenario) => scenario.ReadAsync(async database =>
    {
        const int NextLoginGeneration = 3;
        var context = await database.Set<BrowserContext>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(NextLoginGeneration, context.Generation);
        Assert.Null(context.TicketDigest);
        var pending = await database.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(row => row.State == BrowserIntentState.Pending, TestContext.Current.CancellationToken);
        Assert.Equal(context.Generation, pending.Generation);
        Assert.Equal(1, await database.Set<BrowserSessionAudit>().CountAsync(row => row.Operation == BrowserMutation.Logout, TestContext.Current.CancellationToken));
        return true;
    });

    /// <summary>Discovers each actual database lock order on each real provider.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                    from order in Orders
                                                                    select new TheoryDataRow<string, bool>(provider, order).WithTrait(ProviderTrait, provider);
}
