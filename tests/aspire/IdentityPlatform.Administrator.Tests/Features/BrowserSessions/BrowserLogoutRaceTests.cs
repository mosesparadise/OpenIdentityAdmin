using System.Net;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires provider-observed ordering between a real product callback and deliberate protected logout.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLogoutRaceTests(BootstrapFixture fixture)
{
    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly bool[] Orders = [false, true];
    private const string ProviderTrait = "Provider";

    /// <summary>Anonymous pending cancellation either prevents establishment or revokes its committed ticket; response arrival cannot restore the older generation.</summary>
    [Theory, MemberData(nameof(Variants))]
    public async Task Given_CallbackAndProtectedLogout_When_BrowserLockContends_Then_LogoutLeavesNoUsableOlderSession(string provider, bool callbackFirst)
    {
        var callbackProbe = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        var replacementProbe = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        var pause = new BrowserCallbackPause();
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, services =>
        {
            callbackProbe.Configure(services);
            if (!callbackFirst)
            {
                pause.Configure(services);
            }
        });
        var callback = await scenario.CallbackAsync();
        var cookies = scenario.Cookies.GetCookieHeader(callback);
        var antiforgery = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        await using var peer = await scenario.PeerAsync(replacementProbe.Configure);
        using var browser = peer.Browser(scenario.Cookies);
        var locked = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var winner = callbackFirst ? callbackProbe : replacementProbe;
        var loser = callbackFirst ? replacementProbe : callbackProbe;
        winner.Locked = locked;
        winner.Release = release.Task;
        loser.BeforeLock = waiting;
        Func<Task<HttpResponseMessage>> complete = () => BrowserCallbackTests.ReplayAsync(scenario.Host, callback, cookies);
        Func<Task<HttpResponseMessage>> replace = () => BrowserLogoutHttp.PostAsync(browser, antiforgery);
        var results = callbackFirst
            ? await NativeBlockedRace.InvokeAsync(complete, locked.Task, replace, waiting.Task, release, scenario.Database.ObserveAsync)
            : await pause.RunAsync(complete, resume => NativeBlockedRace.InvokeAsync(replace, locked.Task, resume, waiting.Task, release, scenario.Database.ObserveAsync));
        using var first = results[0];
        using var second = results[1];
        await BrowserLogoutHttp.CommittedAsync(callbackFirst ? second : first);
        Assert.Equal(callbackFirst ? HttpStatusCode.SeeOther : HttpStatusCode.ServiceUnavailable, (callbackFirst ? first : second).StatusCode);
        await RequireWinningGenerationAsync(scenario, callbackFirst);
        Assert.Equal(1, scenario.Host.Bridge.TokenSends);
        Assert.Equal(0, peer.Bridge.TokenSends);
    }

    /// <summary>Reads the committed winner and ticket revocation independently of the order in which HTTP cookies could arrive.</summary>
    private static Task<bool> RequireWinningGenerationAsync(BrowserLoginFixture scenario, bool established) => scenario.ReadAsync(async database =>
    {
        const int LogoutGeneration = 2;
        var browser = await database.Set<BrowserContext>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LogoutGeneration, browser.Generation);
        Assert.Null(browser.TicketDigest);
        var tickets = await database.Set<AdminSessionTicket>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
        if (established)
        {
            Assert.NotNull(Assert.Single(tickets).RevokedAt);
        }
        else
        {
            Assert.Empty(tickets);
        }
        var audit = await database.Set<BrowserSessionAudit>().AsNoTracking().SingleAsync(row => row.Operation == BrowserMutation.Logout, TestContext.Current.CancellationToken);
        Assert.Null(audit.Subject);
        Assert.Null(audit.SourceSession);
        Assert.Equal(browser.CorrelationId, audit.CorrelationId);
        return true;
    });

    /// <summary>Discovers both actual database commit orders on PostgreSQL and SQL Server.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Variants => from provider in ProviderNames
                                                                       from order in Orders
                                                                       select new TheoryDataRow<string, bool>(provider, order).WithTrait(ProviderTrait, provider);
}
