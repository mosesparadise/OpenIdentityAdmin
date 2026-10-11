using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

internal static class BrowserJourneys
{
    public static async Task RequireEmptyStorageAsync(IPage page)
    {
        Assert.True(await page.EvaluateAsync<bool>("Object.keys(sessionStorage).length === 0 && Object.keys(localStorage).every(key => key === 'identity-theme' && ['system', 'light', 'dark'].includes(localStorage.getItem(key)))"), "Unexpected browser storage content.");
    }

    public static async Task LoginAsync(IPage page, IssuerEnvironment environment, TestFlow flow, bool javaScript = true)
    {
        var callbackNavigations = 0;
        var credentialSnapshots = 0;
        void ObserveCallback(object? sender, IRequest request)
        {
            if (request.IsNavigationRequest && request.Url.StartsWith(environment.RelyingParty.Callback + "?", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref callbackNavigations);
            }
        }
        page.Request += ObserveCallback;
        flow.InteractionId = await page.Locator("input[name=Interaction]").InputValueAsync();
        // Observe the committed credential event before the continuation request can issue a code.
        await page.RouteAsync("**/connect/authorize?**", async route =>
        {
            await using var scope = environment.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var interaction = await database.Set<LoginInteraction>().SingleAsync(value => value.Id == flow.InteractionId, TestContext.Current.CancellationToken);
            Assert.True(!interaction.Consumed, "Credential snapshot must precede native authorization consumption.");
            Interlocked.Increment(ref credentialSnapshots);
            var session = await database.Set<LocalIdentitySession>().SingleAsync(value => value.Id == interaction.CompletedSessionId, TestContext.Current.CancellationToken);
            flow.ExpectedSessionId = session.Id;
            flow.ExpectedAuthenticatedAt = session.AuthenticatedAt;
            await route.ContinueAsync();
        });
        await page.Locator("#username").FillAsync(environment.Username);
        await page.Locator("#password").FillAsync(environment.Password);
        await page.Locator("#password").PressAsync("Enter");
        if (!javaScript)
        {
            await Assertions.Expect(page.Locator("#continue-link")).ToBeVisibleAsync();
            var target = await page.Locator("#continue-link").GetAttributeAsync("href");
            Assert.True(target is not null && new Uri(environment.Issuer, target).GetLeftPart(UriPartial.Authority) == environment.Issuer.GetLeftPart(UriPartial.Authority), "Manual continuation must remain on the issuer origin.");
            await page.Locator("#continue-link").ClickAsync();
        }
        await page.WaitForURLAsync(environment.RelyingParty.Callback + "**");
        Assert.True(await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken), "The relying party must independently validate the code exchange.");
        Assert.Equal(1, Volatile.Read(ref credentialSnapshots));
        Assert.True(!string.IsNullOrEmpty(flow.ExpectedSessionId) && flow.ExpectedAuthenticatedAt.HasValue, "The credential event snapshot must exist before token validation.");
        Assert.Equal(1, Volatile.Read(ref callbackNavigations));
        page.Request -= ObserveCallback;
        Assert.Equal(1, flow.CallbackCount);
        Assert.Equal(1, flow.ExchangeCount);
        await page.UnrouteAsync("**/connect/authorize?**");
    }

    public static async Task RequireNoTokensAsync(IPage page, IBrowserContext context, IssuerEnvironment environment, params TestFlow[] flows)
    {
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
