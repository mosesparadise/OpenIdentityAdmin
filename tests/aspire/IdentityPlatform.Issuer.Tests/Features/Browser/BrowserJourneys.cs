using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

internal enum BrowserJourneyRole
{
    Ordinary, InitialSession, FirstTab, SecondTab, Recovery
}

internal static class BrowserJourneys
{
    public static async Task RequireEmptyStorageAsync(IPage page)
    {
        Assert.True(await page.EvaluateAsync<bool>("Object.keys(sessionStorage).length === 0 && Object.keys(localStorage).every(key => key === 'identity-theme' && ['system', 'light', 'dark'].includes(localStorage.getItem(key)))"), "Unexpected browser storage content.");
    }

    public static async Task LoginAsync(IPage page, IssuerEnvironment environment, TestFlow flow, bool javaScript = true, BrowserJourneyRole role = BrowserJourneyRole.Ordinary)
    {
        BrowserEvidence.Mark(BrowserPhase.CredentialEntry);
        // Model the user selecting the tab before entering credentials; background-tab scheduling differs by engine.
        await page.BringToFrontAsync();
        var callbackNavigations = 0;
        var credentialSnapshots = 0;
        var loginStatus = 0;
        void ObserveLoginResponse(object? sender, IResponse response)
        {
            if (response.Request.Method == "POST" && new Uri(response.Url).GetLeftPart(UriPartial.Authority) == environment.Issuer.GetLeftPart(UriPartial.Authority) && new Uri(response.Url).AbsolutePath == "/account/login")
            {
                Interlocked.Exchange(ref loginStatus, response.Status);
            }
        }
        void ObserveCallback(object? sender, IRequest request)
        {
            if (request.IsNavigationRequest && request.Url.StartsWith(environment.RelyingParty.Callback + "?", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref callbackNavigations);
            }
        }
        page.Request += ObserveCallback;
        page.Response += ObserveLoginResponse;
        try
        {
            flow.InteractionId = await page.Locator("input[name=Interaction]").InputValueAsync();
            // Observe the committed credential event before the continuation request can issue a code.
            await page.RouteAsync("**/connect/authorize?**", async route =>
            {
                await using var scope = environment.Services.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var interaction = await database.Set<LoginInteraction>().SingleAsync(value => value.Id == flow.InteractionId, TestContext.Current.CancellationToken);
                Assert.True(!interaction.Consumed, "Credential snapshot must precede native authorization consumption.");
                var session = await database.Set<LocalIdentitySession>().SingleAsync(value => value.Id == interaction.CompletedSessionId, TestContext.Current.CancellationToken);
                flow.ExpectedSessionId = session.Id;
                flow.ExpectedAuthenticatedAt = session.AuthenticatedAt;
                Interlocked.Increment(ref credentialSnapshots);
                await route.ContinueAsync();
            });
            await page.Locator("#username").FillAsync(environment.Username);
            await page.Locator("#password").FillAsync(environment.Password);
            BrowserEvidence.Mark(BrowserPhase.CredentialSubmission);
            await page.Locator("#password").PressAsync("Enter");
            if (!javaScript)
            {
                BrowserEvidence.Mark(BrowserPhase.ManualContinuation);
                await Assertions.Expect(page.Locator("#continue-link")).ToBeVisibleAsync();
                var target = await page.Locator("#continue-link").GetAttributeAsync("href");
                Assert.True(target is not null && new Uri(environment.Issuer, target).GetLeftPart(UriPartial.Authority) == environment.Issuer.GetLeftPart(UriPartial.Authority), "Manual continuation must remain on the issuer origin.");
                await page.Locator("#continue-link").ClickAsync();
            }
            BrowserEvidence.Mark(BrowserPhase.Callback);
            try
            {
                await WaitForCallbackDocumentAsync(page, environment.RelyingParty.Callback, flow.State);
            }
            catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
            {
                var current = new Uri(page.Url);
                var states = QueryHelpers.ParseQuery(current.Query).GetValueOrDefault("state");
                var matches = current.GetLeftPart(UriPartial.Path) == environment.RelyingParty.Callback && states.Count == 1 && states[0] == flow.State;
                throw CallbackFailure(page.Url, role, Volatile.Read(ref loginStatus), Volatile.Read(ref credentialSnapshots), Volatile.Read(ref callbackNavigations),
                    flow.Completion.Task.IsCompleted, flow.Completion.Task.IsCompletedSuccessfully && flow.Completion.Task.Result, matches);
            }
            BrowserEvidence.Mark(BrowserPhase.CallbackValidation);
            Assert.True(await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken), "The relying party must independently validate the code exchange.");
            Assert.True(await page.Locator("body").InnerTextAsync() == "Signed in successfully", "The callback document must contain the relying party's validated success response.");
            Assert.Equal(1, Volatile.Read(ref credentialSnapshots));
            Assert.True(!string.IsNullOrEmpty(flow.ExpectedSessionId) && flow.ExpectedAuthenticatedAt.HasValue, "The credential event snapshot must exist before token validation.");
            Assert.Equal(1, Volatile.Read(ref callbackNavigations));
            Assert.Equal(1, flow.CallbackCount);
            Assert.Equal(1, flow.ExchangeCount);
        }
        finally
        {
            page.Request -= ObserveCallback;
            page.Response -= ObserveLoginResponse;
            await page.UnrouteAsync("**/connect/authorize?**");
        }
    }

    internal static async Task WaitForCallbackDocumentAsync(IPage page, string callback, string state, float timeout = 20_000)
    {
        var target = new Uri(callback);
        // Observe current browser state atomically, without a cached-state/event-subscription gap.
        await using var result = await page.WaitForFunctionAsync("""
            expected => {
                const states = new URLSearchParams(location.search).getAll('state');
                return location.origin === expected.origin && location.pathname === expected.path &&
                    states.length === 1 && states[0] === expected.state && document.readyState === 'complete';
            }
            """, new
        {
            origin = target.GetLeftPart(UriPartial.Authority),
            path = target.AbsolutePath,
            state
        }, new()
        {
            Timeout = timeout
        });
    }

    internal static Xunit.Sdk.XunitException CallbackFailure(string url, BrowserJourneyRole role, int loginStatus, int snapshots, int callbacks, bool completed, bool validated, bool matches)
    {
        var location = new Uri(url).AbsolutePath switch
        {
            "/account/login" => "login",
            "/account/complete" => "completion",
            "/connect/authorize" => "authorization",
            "/callback" => "callback",
            _ => "other"
        };
        return new Xunit.Sdk.XunitException($"Browser callback wait failed for {role} at {location}; login status {loginStatus}, credential snapshots {snapshots}, callback navigations {callbacks}, relying-party completion {completed}, validated result {validated}, exact callback {matches}. Sensitive diagnostics suppressed.");
    }

    public static async Task RequireNoTokensAsync(IPage page, IBrowserContext context, IssuerEnvironment environment, params TestFlow[] flows)
    {
        BrowserEvidence.Mark(BrowserPhase.StorageInspection);
        await page.BringToFrontAsync();
        await RequireEmptyStorageAsync(page);
        var state = await context.StorageStateAsync(new()
        {
            IndexedDB = true
        });
        Assert.True(flows.All(flow => flow.ContainsNoTokens(state)), "Token material reached persistent browser storage.");
        // Same tab is required: sessionStorage is scoped by origin AND top-level browsing context.
        await page.GotoAsync(new Uri(environment.Issuer, "/account/login").AbsoluteUri);
        await RequireEmptyStorageAsync(page);
        var session = await page.EvaluateAsync<string>("JSON.stringify(sessionStorage)");
        Assert.True(flows.All(flow => flow.ContainsNoTokens(session)), "Token material reached issuer session storage.");
    }
}
