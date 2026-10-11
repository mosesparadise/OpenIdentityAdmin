using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Restart;

[Collection("lifecycle")]
public sealed class SessionContinuityTests(LifecycleFixture fixture)
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Original_cookie_survives_process_replacement_without_login_or_ticket_refresh(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        var original = await environment.AuthorizeAsync(browser, exchange: true);
        var sessions = await RestartEvidence.SessionsAsync(environment);
        var ticket = cookies.GetCookies(environment.Issuer)[PilotProfile.SessionCookie]!.Value;
        await environment.RestartIssuerAsync();
        await RestartEvidence.SilentAsync(environment, new CookieContainer(), original, allowed: false);
        var corrupt = new CookieContainer();
        corrupt.Add(environment.Issuer, new Cookie(PilotProfile.SessionCookie, "invalid-ticket", "/") { Secure = true });
        await RestartEvidence.SilentAsync(environment, corrupt, original, allowed: false);
        await RestartEvidence.SilentAsync(environment, cookies, original, allowed: true);
        Assert.True(sessions == await RestartEvidence.SessionsAsync(environment), "Silent authorization replaced or mutated persisted sessions.");
        Assert.True(cookies.GetCookies(environment.Issuer)[PilotProfile.SessionCookie]!.Value == ticket, "The retained ticket changed.");
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Original_pending_form_keeps_antiforgery_and_browser_binding_after_restart(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var owner = IssuerEnvironment.CreateBrowserClient();
        using var foreign = IssuerEnvironment.CreateBrowserClient();
        var login = await TestLogin.BeginAsync(environment, owner, exchange: true);
        var foreignForm = await TestLogin.BeginAsync(environment, foreign);
        await environment.RestartIssuerAsync();
        var before = await RestartEvidence.StateAsync(environment);
        using (var invalid = await login.SubmitAsync(environment, owner, antiforgery: "invalid-proof"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        await RestartEvidence.RequireUnchangedAsync(environment, before);
        using (var invalid = await login.SubmitAsync(environment, foreign, antiforgery: foreignForm.Antiforgery))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        await RestartEvidence.RequireUnchangedAsync(environment, before);
        var continuation = await login.CompleteAsync(environment, owner);
        await login.FollowAsync(environment, owner, continuation);
        Assert.True((await RestartEvidence.InteractionAsync(environment, login.Interaction)).Consumed);
        before = await RestartEvidence.StateAsync(environment);
        using var replay = await login.SubmitAsync(environment, owner);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        await RestartEvidence.RequireUnchangedAsync(environment, before);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Completed_continuation_retains_owner_binding_and_single_consumption_after_restart(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var owner = IssuerEnvironment.CreateBrowserClient();
        using var foreign = IssuerEnvironment.CreateBrowserClient(autoRedirect: false);
        var login = await TestLogin.BeginAsync(environment, owner, exchange: true);
        var continuation = await login.CompleteAsync(environment, owner);
        var completed = await RestartEvidence.InteractionAsync(environment, login.Interaction);
        await environment.RestartIssuerAsync();
        var before = await RestartEvidence.StateAsync(environment);
        using (var denied = await foreign.GetAsync(continuation, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            var location = denied.Headers.Location;
            Assert.True(location is not null && location.IsAbsoluteUri && location.GetLeftPart(UriPartial.Path) == environment.RelyingParty.Callback,
                "Foreign continuation did not return a native denial to the registered callback.");
            var query = QueryHelpers.ParseQuery(location.Query);
            Assert.True(query["error"] == "access_denied" && query["state"] == login.Flow.State && !query.ContainsKey("code"),
                "Foreign continuation did not preserve denial/state binding.");
            Assert.False(denied.Headers.TryGetValues("Set-Cookie", out var headers) && headers.Any(value => value.StartsWith(PilotProfile.SessionCookie + "=", StringComparison.Ordinal)));
            Assert.False(login.Flow.Completion.Task.IsCompleted, "Foreign browser reached the owner's callback.");
        }
        await RestartEvidence.RequireUnchangedAsync(environment, before);
        await login.FollowAsync(environment, owner, continuation);
        var consumed = await RestartEvidence.InteractionAsync(environment, login.Interaction);
        Assert.True(consumed.Consumed && consumed.CompletedSessionId == completed.CompletedSessionId && consumed.CompletedUserId == completed.CompletedUserId,
            "Continuation changed its completed identity.");
        before = await RestartEvidence.StateAsync(environment);
        using var replay = await owner.GetAsync(continuation, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(1, login.Flow.CallbackCount);
        await RestartEvidence.RequireUnchangedAsync(environment, before);
    }
}
