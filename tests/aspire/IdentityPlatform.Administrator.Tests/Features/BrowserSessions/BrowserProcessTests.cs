using System.Net;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires actual exited client processes to reopen genuine B3 state, native antiforgery and retained protection files.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserProcessTests(BootstrapFixture fixture)
{

    /// <summary>No live root or imported protection object crosses the child boundary; actual B3 authority survives restart and logout remains durable afterward.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_GenuineProductSession_When_ExitedProcessesReopenKeysAndPerformLogout_Then_OnlyRetainedKeysCanMutateAndOldCookiesStayDenied(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var login = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        var original = scenario.Cookies.GetCookieHeader(scenario.Profile.ClientUri);
        var first = await NativeClientProcess.RunAsync(scenario.ProcessInput(BrowserProcessOperation.Status, original));
        BrowserProcessAssertions.RequireStatus(first.Reply, authenticated: true, scenario.Issuer.Account.ActorId);
        using var material = new BffSessionFixture();
        using var initialize = material.Protection();
        var before = await scenario.SnapshotAsync();
        var missing = await NativeClientProcess.RunAsync(scenario.ProcessInput(BrowserProcessOperation.Status, original, retained: material.Settings()));
        BrowserProcessAssertions.RequireStatus(missing.Reply, authenticated: false);
        Assert.Equal(before, await scenario.SnapshotAsync());
        var form = await NativeClientProcess.RunAsync(scenario.ProcessInput(BrowserProcessOperation.LogoutForm, original));
        BrowserProcessAssertions.RequireLocal(form.Reply, HttpStatusCode.OK);
        var token = AdministratorJourney.Input(form.Reply.BrowserBody!, AdministratorJourney.Antiforgery);
        foreach (var cookie in form.Reply.Cookies)
        {
            scenario.Cookies.SetCookies(scenario.Profile.ClientUri, cookie);
        }
        var protectedCookies = scenario.Cookies.GetCookieHeader(scenario.Profile.ClientUri);
        var protectedSnapshot = await scenario.SnapshotAsync();
        var denied = await NativeClientProcess.RunAsync(scenario.ProcessInput(BrowserProcessOperation.LogoutSubmit, protectedCookies, token, material.Settings()));
        BrowserProcessAssertions.RequireLocal(denied.Reply, HttpStatusCode.BadRequest);
        Assert.True(denied.Reply.Cookies.Length == 0);
        Assert.Equal(protectedSnapshot, await scenario.SnapshotAsync());
        var logout = await NativeClientProcess.RunAsync(scenario.ProcessInput(BrowserProcessOperation.LogoutSubmit,
            scenario.Cookies.GetCookieHeader(scenario.Profile.ClientUri), token));
        BrowserProcessAssertions.RequireLocal(logout.Reply, HttpStatusCode.OK);
        BrowserProcessAssertions.RequireCommitted(logout.Reply);
        await BrowserLogoutTests.RequireCommittedAsync(scenario, authenticated: true);
        var last = await NativeClientProcess.RunAsync(scenario.ProcessInput(BrowserProcessOperation.Status, original));
        BrowserProcessAssertions.RequireStatus(last.Reply, authenticated: false);
        var generations = new[] { first.Generation, missing.Generation, form.Generation, denied.Generation, logout.Generation, last.Generation };
        Assert.Equal(generations.Length, generations.Distinct().Count());
        Assert.Equal(1, scenario.Host.Bridge.TokenSends);
        Assert.Equal(1, scenario.Host.Bridge.ParSends);
    }

}
