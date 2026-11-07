using System.Net;
using System.Text.RegularExpressions;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Runs the real pilot password, protected continuation and callback over TLS, retaining independently persisted session expectations.</summary>
internal static class PilotReplayJourney
{
    private const string Login = "/account/login";
    private const string Complete = "/account/complete";
    private const string Authorize = "/connect/authorize";
    private const string Username = "synthetic-browser-pilot";
    private const string Interaction = "Interaction";
    private const string Antiforgery = "__RequestVerificationToken";
    private const int CompletionSeconds = 5;
    private static readonly Regex Continuation = new("id=\"continue-link\" href=\"([^\"]+)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Creates a genuine code with S256/callback correlation before the replay-ordering barrier is armed.</summary>
    internal static async Task<TestFlow> AuthorizeAsync(BrowserHttpsFixture environment, TestRelyingParty client)
    {
        var token = TestContext.Current.CancellationToken;
        client.Issuer = environment.Profile.Issuer;
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var flow = client.Prepare();
        using var login = await browser.GetAsync(client.StartUri(flow), token);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(Login, login.RequestMessage!.RequestUri!.AbsolutePath);
        var html = await login.Content.ReadAsStringAsync(token);
        flow.InteractionId = IssuerEnvironment.InputValue(html, Interaction);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Interaction] = flow.InteractionId,
            [Antiforgery] = IssuerEnvironment.InputValue(html, Antiforgery),
            [BrowserIssuerHttp.Username] = Username,
            [BrowserIssuerHttp.Password] = environment.Issuer.Account.Password
        });
        using var completed = await browser.PostAsync(new Uri(client.Issuer, Login), form, token);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(Complete, completed.RequestMessage!.RequestUri!.AbsolutePath);
        await BindIdentityAsync(environment, client, flow);
        var match = Continuation.Match(await completed.Content.ReadAsStringAsync(token));
        Assert.True(match.Success);
        var continuation = new Uri(client.Issuer, WebUtility.HtmlDecode(match.Groups[1].Value));
        Assert.Equal(client.Issuer.GetLeftPart(UriPartial.Authority), continuation.GetLeftPart(UriPartial.Authority));
        Assert.Equal(Authorize, continuation.AbsolutePath);
        using var callback = await browser.GetAsync(continuation, token);
        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.True(await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(CompletionSeconds), token));
        Assert.Equal(1, flow.CallbackCount);
        Assert.Equal(0, flow.ExchangeCount);
        return flow;
    }

    /// <summary>Freezes actual password-session subject/time before exchange so token validation cannot derive expectations from the returned token.</summary>
    private static async Task BindIdentityAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow flow)
    {
        await using var scope = environment.IssuerServices.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.LegacyPilot);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var interaction = await database.Set<LoginInteraction>().AsNoTracking().SingleAsync(row => row.Id == flow.InteractionId, token);
        Assert.NotNull(interaction.CompletedSessionId);
        var session = await database.Set<LocalIdentitySession>().AsNoTracking().SingleAsync(row => row.Id == interaction.CompletedSessionId, token);
        Assert.Equal(interaction.CompletedUserId, session.UserId);
        flow.ExpectedSessionId = session.Id;
        flow.ExpectedAuthenticatedAt = session.AuthenticatedAt;
        client.ExpectedIdentity = (_, _) => Task.FromResult((session.UserId, session.AuthenticatedAt));
    }
}
