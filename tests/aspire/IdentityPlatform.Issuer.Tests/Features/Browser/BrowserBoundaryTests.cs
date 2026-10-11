using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

[Collection("issuer")]
[Trait("Category", "Browser")]
public sealed class BrowserBoundaryTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Changes = ["missing-antiforgery", "invalid-antiforgery", "foreign-interaction"];
    public static IEnumerable<TheoryDataRow<string, string>> FormCases =>
        from provider in Providers from change in Changes select new TheoryDataRow<string, string>(provider, change).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(FormCases))]
    public async Task Form_boundaries_reject_modified_requests_without_burning_legitimate_interactions(string provider, string change)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            await using var owner = await BrowserSession.CreateAsync();
            var ownerPage = await owner.Context.NewPageAsync();
            var ownerFlow = environment.RelyingParty.Prepare(exchange: true);
            await ownerPage.GotoAsync(environment.RelyingParty.StartUri(ownerFlow).AbsoluteUri);
            var ownerId = await ownerPage.Locator("input[name=Interaction]").InputValueAsync();
            await using var other = change == "foreign-interaction" ? await BrowserSession.CreateAsync() : null;
            var page = ownerPage;
            var flow = ownerFlow;
            var context = owner.Context;
            if (change == "foreign-interaction")
            {
                page = await other!.Context.NewPageAsync();
                context = other.Context;
                flow = environment.RelyingParty.Prepare(exchange: true);
                await page.GotoAsync(environment.RelyingParty.StartUri(flow).AbsoluteUri);
            }
            var originalUrl = page.Url;
            var ownId = await page.Locator("input[name=Interaction]").InputValueAsync();
            var originalToken = await page.Locator("input[name=__RequestVerificationToken]").InputValueAsync();
            var sessionsBefore = await SessionCountAsync(environment);
            if (change == "foreign-interaction")
            {
                await page.Locator("input[name=Interaction]").EvaluateAsync("(element, value) => element.value = value", ownerId);
            }
            else
            {
                await page.Locator("input[name=__RequestVerificationToken]").EvaluateAsync(change == "missing-antiforgery"
                    ? "element => element.remove()" : "element => element.value = 'invalid-antiforgery-token'");
            }
            await FillCredentialsAsync(page, environment);
            var response = await page.RunAndWaitForResponseAsync(() => page.Locator("#password").PressAsync("Enter"),
                value => value.Request.Method == "POST" && new Uri(value.Url).AbsolutePath == "/account/login");
            Assert.Equal(400, response.Status);
            SafeAssertions.ContainsNoSensitiveMaterial(await response.TextAsync(), environment.Password, originalToken);
            Assert.Equal(sessionsBefore, await SessionCountAsync(environment));
            Assert.True(!(await context.CookiesAsync()).Any(cookie => cookie.Name == PilotProfile.SessionCookie), "Rejected form must not authenticate the browser.");
            Assert.True(ownerFlow.Code.Length == 0 && flow.Code.Length == 0, "Rejected form issued a code.");
            await RequirePendingAsync(environment, ownerId, ownId);
            await page.GotoAsync(originalUrl);
            // A foreign-interaction rejection must not invalidate the otherwise valid form token.
            await page.Locator("input[name=__RequestVerificationToken]").EvaluateAsync("(element, value) => element.value = value", originalToken);
            await BrowserJourneys.LoginAsync(page, environment, flow);
            if (change == "foreign-interaction")
            {
                await BrowserJourneys.LoginAsync(ownerPage, environment, ownerFlow);
            }
        });
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Authenticated_tabs_keep_independent_correlations_and_complete_their_original_flows(string provider)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            RequireDifferentSites(environment);
            await using var session = await BrowserSession.CreateAsync();
            var firstPage = await session.Context.NewPageAsync();
            var original = environment.RelyingParty.Prepare(exchange: true);
            BrowserEvidence.Mark(BrowserPhase.InitialNavigation);
            await firstPage.GotoAsync(environment.RelyingParty.StartUri(original).AbsoluteUri);
            await BrowserJourneys.LoginAsync(firstPage, environment, original, role: BrowserJourneyRole.InitialSession);
            var first = environment.RelyingParty.Prepare(exchange: true, prompt: "login");
            BrowserEvidence.Mark(BrowserPhase.FirstTabNavigation);
            await StartFromRelyingPartyAsync(firstPage, environment, first);
            var antiforgeryCookie = (await session.Context.CookiesAsync()).Single(cookie => cookie.Name == "__Host-pilot-antiforgery");
            Assert.True(antiforgeryCookie.Secure && antiforgeryCookie.HttpOnly && antiforgeryCookie.Path == "/" && antiforgeryCookie.SameSite == SameSiteAttribute.Lax,
                "Antiforgery must retain protected host-cookie attributes and safe cross-site navigation semantics.");
            var firstAntiforgery = antiforgeryCookie.Value;
            var secondPage = await session.Context.NewPageAsync();
            var second = environment.RelyingParty.Prepare(exchange: true, prompt: "login");
            BrowserEvidence.Mark(BrowserPhase.SecondTabNavigation);
            await StartFromRelyingPartyAsync(secondPage, environment, second);
            var firstId = await firstPage.Locator("input[name=Interaction]").InputValueAsync();
            var secondId = await secondPage.Locator("input[name=Interaction]").InputValueAsync();
            var secondAntiforgery = (await session.Context.CookiesAsync()).Single(cookie => cookie.Name == "__Host-pilot-antiforgery").Value;
            Assert.True(firstAntiforgery.Length > 0 && firstAntiforgery == secondAntiforgery, "Opening another pending form must preserve the first form's antiforgery cookie.");
            Assert.True(firstId != secondId && first.State != second.State && first.Nonce != second.Nonce, "Tabs must retain independent request bindings.");
            await BrowserJourneys.LoginAsync(firstPage, environment, first, role: BrowserJourneyRole.FirstTab);
            var cookies = await session.Context.CookiesAsync();
            Assert.True(!cookies.Any(cookie => cookie.Name == PilotProfile.CorrelationPrefix + firstId), "Completed tab must clear its own correlation.");
            Assert.True(cookies.Any(cookie => cookie.Name == PilotProfile.CorrelationPrefix + secondId), "Completing one tab must retain the other's correlation.");
            await BrowserJourneys.LoginAsync(secondPage, environment, second, role: BrowserJourneyRole.SecondTab);
            Assert.True(first.ExpectedSessionId != second.ExpectedSessionId, "Each fresh credential event must retain its own session.");
            await using var scope = environment.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.Equal(2, await database.Set<LoginInteraction>().CountAsync(value => (value.Id == firstId || value.Id == secondId) && value.Consumed, TestContext.Current.CancellationToken));
            await BrowserJourneys.RequireNoTokensAsync(firstPage, session.Context, environment, original, first, second);
            await BrowserJourneys.RequireNoTokensAsync(secondPage, session.Context, environment, original, first, second);
        });
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Cross_site_post_with_a_copied_valid_form_token_cannot_authenticate_or_consume_the_interaction(string provider)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            RequireDifferentSites(environment);
            await using var session = await BrowserSession.CreateAsync();
            var owner = await session.Context.NewPageAsync();
            var flow = environment.RelyingParty.Prepare(exchange: true);
            await StartFromRelyingPartyAsync(owner, environment, flow);
            var id = await owner.Locator("input[name=Interaction]").InputValueAsync();
            var token = await owner.Locator("input[name=__RequestVerificationToken]").InputValueAsync();
            var before = await SessionCountAsync(environment);
            var hostile = await session.Context.NewPageAsync();
            await hostile.GotoAsync(new Uri(environment.RelyingParty.BaseUri, "/alive").AbsoluteUri);
            await hostile.SetContentAsync("<!doctype html><html lang='en'><title>Cross-site form control</title><body></body></html>");
            await hostile.EvaluateAsync("""
                values => {
                    const form = document.createElement('form');
                    form.method = 'post'; form.action = values.action;
                    for (const [name, value] of Object.entries(values.fields)) {
                        const input = document.createElement('input');
                        input.type = 'hidden'; input.name = name; input.value = value;
                        form.append(input);
                    }
                    const submit = document.createElement('button');
                    submit.id = 'submit'; submit.type = 'submit'; submit.textContent = 'Submit';
                    form.append(submit); document.body.append(form);
                }
                """, new
            {
                action = new Uri(environment.Issuer, "/account/login").AbsoluteUri,
                fields = new Dictionary<string, string>
                {
                    ["Interaction"] = id,
                    ["__RequestVerificationToken"] = token,
                    ["Username"] = environment.Username,
                    ["Password"] = environment.Password
                }
            });
            var response = await hostile.RunAndWaitForResponseAsync(() => hostile.Locator("#submit").ClickAsync(),
                value => value.Request.Method == "POST" && new Uri(value.Url) == new Uri(environment.Issuer, "/account/login"));
            var headers = await response.Request.AllHeadersAsync();
            var posted = QueryHelpers.ParseQuery(response.Request.PostData ?? "");
            Assert.True(posted.Count == 4 && posted.GetValueOrDefault("Interaction") == id && posted.GetValueOrDefault("__RequestVerificationToken") == token &&
                posted.GetValueOrDefault("Username") == environment.Username && posted.GetValueOrDefault("Password") == environment.Password,
                "The cross-site control must transmit the exact legitimate form token, interaction and synthetic credentials.");
            Assert.True(headers.GetValueOrDefault("origin") == environment.RelyingParty.BaseUri.GetLeftPart(UriPartial.Authority), "The unsafe request must originate at the distinct relying-party site.");
            Assert.True(!headers.GetValueOrDefault("cookie", "").Contains("__Host-pilot-antiforgery=", StringComparison.Ordinal), "The cross-site unsafe request must not carry the protected antiforgery cookie.");
            Assert.Equal(400, response.Status);
            Assert.True(response.Headers.GetValueOrDefault("content-type", "").StartsWith("text/plain", StringComparison.Ordinal), "Native antiforgery rejection must use the generic plaintext response.");
            var body = await response.TextAsync();
            Assert.True(body == "This request cannot be completed. Return to your application and start a new sign-in.", "The unsafe request must receive the native generic rejection.");
            SafeAssertions.ContainsNoSensitiveMaterial(body, token, environment.Password);
            Assert.Equal(before, await SessionCountAsync(environment));
            Assert.True(!(await session.Context.CookiesAsync()).Any(cookie => cookie.Name == PilotProfile.SessionCookie), "Cross-site rejection must not authenticate the browser.");
            Assert.True(flow.Code.Length == 0 && flow.ExchangeCount == 0, "Cross-site rejection must not issue or exchange a code.");
            await RequirePendingAsync(environment, id);
            await BrowserJourneys.LoginAsync(owner, environment, flow);
        });
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Completed_tab_cannot_adopt_a_replacement_session_and_a_new_flow_recovers(string provider)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            await using var session = await BrowserSession.CreateAsync(javaScript: false);
            var firstPage = await session.Context.NewPageAsync();
            var first = environment.RelyingParty.Prepare(exchange: true, prompt: "login");
            BrowserEvidence.Mark(BrowserPhase.FirstTabNavigation);
            await firstPage.GotoAsync(environment.RelyingParty.StartUri(first).AbsoluteUri);
            var firstId = await firstPage.Locator("input[name=Interaction]").InputValueAsync();
            await FillCredentialsAsync(firstPage, environment);
            await firstPage.Locator("#password").PressAsync("Enter");
            await Assertions.Expect(firstPage.Locator("#continue-link")).ToBeVisibleAsync();
            var continuation = await firstPage.Locator("#continue-link").GetAttributeAsync("href");
            Assert.True(continuation is not null && new Uri(environment.Issuer, continuation).GetLeftPart(UriPartial.Authority) == environment.Issuer.GetLeftPart(UriPartial.Authority), "Continuation must remain on the issuer origin.");
            string? originalSession;
            string? originalUser;
            await using (var scope = environment.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var interaction = await database.Set<LoginInteraction>().SingleAsync(value => value.Id == firstId, TestContext.Current.CancellationToken);
                originalSession = interaction.CompletedSessionId;
                originalUser = interaction.CompletedUserId;
                Assert.True(originalSession is not null && originalUser is not null && !interaction.Consumed, "The paused completion must retain its original credential event.");
            }
            var secondPage = await session.Context.NewPageAsync();
            var second = environment.RelyingParty.Prepare(exchange: true, prompt: "login");
            BrowserEvidence.Mark(BrowserPhase.SecondTabNavigation);
            await secondPage.GotoAsync(environment.RelyingParty.StartUri(second).AbsoluteUri);
            await BrowserJourneys.LoginAsync(secondPage, environment, second, javaScript: false, role: BrowserJourneyRole.SecondTab);
            Assert.Equal(400, (await firstPage.ReloadAsync())!.Status);
            Assert.Equal(0, await firstPage.Locator("#continue-link").CountAsync());
            await firstPage.GotoAsync(new Uri(environment.Issuer, continuation!).AbsoluteUri);
            await firstPage.WaitForURLAsync(environment.RelyingParty.Callback + "**");
            Assert.True(first.Error == "access_denied", "Replaced completion must return the expected native denial.");
            Assert.True(first.Code.Length == 0 && !first.Validated && first.ExchangeCount == 0, "Replaced completion must not issue or exchange a code.");
            await using (var scope = environment.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var interaction = await database.Set<LoginInteraction>().SingleAsync(value => value.Id == firstId, TestContext.Current.CancellationToken);
                Assert.False(interaction.Consumed);
                Assert.True(interaction.CompletedSessionId == originalSession && interaction.CompletedUserId == originalUser && originalSession != second.ExpectedSessionId, "A completed interaction must retain its original session and user.");
                Assert.True(await database.Set<LocalIdentitySession>().AnyAsync(value => value.Id == originalSession && value.Revoked, TestContext.Current.CancellationToken), "The original replaced session must be revoked.");
            }
            var recovery = environment.RelyingParty.Prepare(exchange: true, prompt: "login");
            await firstPage.GotoAsync(environment.RelyingParty.StartUri(recovery).AbsoluteUri);
            await BrowserJourneys.LoginAsync(firstPage, environment, recovery, javaScript: false, role: BrowserJourneyRole.Recovery);
        });
    }

    private static async Task FillCredentialsAsync(IPage page, IssuerEnvironment environment)
    {
        await page.Locator("#username").FillAsync(environment.Username);
        await page.Locator("#password").FillAsync(environment.Password);
    }

    private static void RequireDifferentSites(IssuerEnvironment environment) =>
        Assert.True(environment.Issuer.Host == "localhost" && environment.RelyingParty.BaseUri.Host == "127.0.0.1", "This regression requires the issuer and relying party to use distinct loopback sites.");

    private static async Task StartFromRelyingPartyAsync(IPage page, IssuerEnvironment environment, TestFlow flow)
    {
        await page.BringToFrontAsync();
        await page.GotoAsync(new Uri(environment.RelyingParty.BaseUri, "/alive").AbsoluteUri);
        await page.SetContentAsync("<!doctype html><html lang='en'><title>Relying party</title><body><a id='start'>Sign in</a></body></html>");
        await page.Locator("#start").EvaluateAsync("(element, value) => element.href = value", environment.RelyingParty.StartUri(flow).AbsoluteUri);
        await page.Locator("#start").ClickAsync();
        await Assertions.Expect(page.Locator("input[name=Interaction]")).ToBeAttachedAsync();
    }

    private static async Task<int> SessionCountAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LocalIdentitySession>().CountAsync(TestContext.Current.CancellationToken);
    }

    private static async Task RequirePendingAsync(IssuerEnvironment environment, params string[] ids)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(ids.Distinct(StringComparer.Ordinal).Count(), await database.Set<LoginInteraction>()
            .CountAsync(value => ids.Contains(value.Id) && value.CompletedSessionId == null && !value.Consumed, TestContext.Current.CancellationToken));
    }
}
