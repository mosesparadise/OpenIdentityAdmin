using System.Net;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

[Collection("distribution")]
public sealed class SessionDistributionTests(DistributionFixture fixture)
{
    public static IEnumerable<TheoryDataRow<string, string, string>> Directions => DistributionEvidence.Directions;

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task Original_form_crosses_nodes_with_antiforgery_and_browser_binding(string provider, string source, string target)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var first = environment.NodeClient(source, cookies, autoRedirect: true);
        using var second = environment.NodeClient(target, cookies, autoRedirect: true);
        using var foreign = environment.NodeClient(target, autoRedirect: true);
        var login = await TestLogin.BeginAsync(environment, first);
        var foreignForm = await TestLogin.BeginAsync(environment, foreign);
        var before = await RestartEvidence.StateAsync(environment);
        using (var denied = await login.SubmitAsync(environment, second, antiforgery: "invalid-proof"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }
        await RestartEvidence.RequireUnchangedAsync(environment, before);
        using (var denied = await login.SubmitAsync(environment, foreign, antiforgery: foreignForm.Antiforgery))
        {
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }
        await RestartEvidence.RequireUnchangedAsync(environment, before);
        var continuation = await login.CompleteAsync(environment, second);
        await login.FollowAsync(environment, first, continuation);
        using var tokens = await DistributionEvidence.RedeemAsync(environment, target, login.Flow);
        Assert.True((await RestartEvidence.InteractionAsync(environment, login.Interaction)).Consumed);
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task Original_completed_continuation_crosses_nodes_once_for_its_owner(string provider, string source, string target)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var first = environment.NodeClient(source, cookies, autoRedirect: true);
        using var second = environment.NodeClient(target, cookies, autoRedirect: true);
        using var foreign = environment.NodeClient(target);
        var login = await TestLogin.BeginAsync(environment, first);
        var continuation = await login.CompleteAsync(environment, first);
        var completed = await RestartEvidence.InteractionAsync(environment, login.Interaction);
        var before = await RestartEvidence.StateAsync(environment);
        using (var denied = await foreign.GetAsync(continuation, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            var location = denied.Headers.Location;
            Assert.True(location is not null && location.IsAbsoluteUri && location.GetLeftPart(UriPartial.Path) == environment.RelyingParty.Callback);
            var query = QueryHelpers.ParseQuery(location.Query);
            Assert.True(query["error"] == "access_denied" && query["state"] == login.Flow.State && !query.ContainsKey("code"));
            Assert.False(login.Flow.Completion.Task.IsCompleted);
        }
        await RestartEvidence.RequireUnchangedAsync(environment, before);
        await login.FollowAsync(environment, second, continuation);
        var consumed = await RestartEvidence.InteractionAsync(environment, login.Interaction);
        Assert.True(consumed.Consumed && consumed.CompletedSessionId == completed.CompletedSessionId && consumed.CompletedUserId == completed.CompletedUserId);
        before = await RestartEvidence.StateAsync(environment);
        using (var replay = await second.GetAsync(continuation, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        }
        await RestartEvidence.RequireUnchangedAsync(environment, before);
        Assert.Equal(1, login.Flow.CallbackCount);
        using var tokens = await DistributionEvidence.RedeemAsync(environment, source, login.Flow);
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task Original_cookie_silently_crosses_nodes_without_reauthentication(string provider, string source, string target)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = environment.NodeClient(source, cookies, autoRedirect: true);
        var original = await environment.AuthorizeAsync(browser);
        using var originalTokens = await DistributionEvidence.RedeemAsync(environment, source, original);
        var sessions = await RestartEvidence.SessionsAsync(environment);
        var ticket = cookies.GetCookies(environment.Issuer)[PilotProfile.SessionCookie]!.Value;
        await DistributionEvidence.SilentAsync(environment, target, new CookieContainer(), original, "login_required");
        await DistributionEvidence.SilentAsync(environment, target, cookies, original);
        Assert.True(sessions == await RestartEvidence.SessionsAsync(environment), "Cross-node continuity changed the session inventory.");
        Assert.True(ticket == cookies.GetCookies(environment.Issuer)[PilotProfile.SessionCookie]!.Value, "Cross-node continuity changed the original ticket.");
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task Surviving_node_uses_original_state_while_opposite_process_is_stopped(string provider, string source, string target)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = environment.NodeClient(source, cookies, autoRedirect: true);
        var original = await environment.AuthorizeAsync(browser);
        using var originalTokens = await DistributionEvidence.RedeemAsync(environment, source, original);
        using var stoppedTransport = environment.NodeClient(source);
        var sessions = await RestartEvidence.SessionsAsync(environment);
        await environment.StopNodeAsync(source, TestContext.Current.CancellationToken);
        try
        {
            await environment.RequireStoppedSocketAsync(source);
            await Assert.ThrowsAsync<Xunit.Sdk.FalseException>(async () =>
            {
                using var response = await stoppedTransport.GetAsync(new Uri(environment.Issuer, "/health"), TestContext.Current.CancellationToken);
            });
            Assert.Throws<Xunit.Sdk.FalseException>(() => environment.NodeClient(source));
            await DistributionEvidence.SilentAsync(environment, target, cookies, original);
            Assert.True(sessions == await RestartEvidence.SessionsAsync(environment));
        }
        finally
        {
            using var restoration = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            await environment.StartNodeAsync(source, restoration.Token);
        }
        await Assert.ThrowsAsync<Xunit.Sdk.FalseException>(async () =>
        {
            using var response = await stoppedTransport.GetAsync(new Uri(environment.Issuer, "/health"), TestContext.Current.CancellationToken);
        });
        await DistributionEvidence.SilentAsync(environment, source, cookies, original);
    }
}
