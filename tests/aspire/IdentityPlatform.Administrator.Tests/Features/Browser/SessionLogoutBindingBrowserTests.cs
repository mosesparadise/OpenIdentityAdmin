using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Preserves the actual browser session and exact native destination under missing and substituted issuer authority.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserSessions")]
public sealed class SessionLogoutBindingBrowserTests(BootstrapFixture fixture)
{
    private static readonly BrowserHttpsCounts ClientCounts = new(3, 5, 5, 6);
    private static readonly BrowserHttpsCounts IssuerCounts = new(6, 2, 6, 7);
    private const string Client = "client_id";
    private const string Hint = "logout_hint";
    private const string Return = "post_logout_redirect_uri";
    private const string ForeignClient = "unregistered-browser-client";
    private const string ForeignReturn = "https://unregistered.example.test/return";

    /// <summary>Missing or another genuine issuer cookie and altered native fields never revoke either source; the original protected form still revokes only its source.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_TwoRealBrowserSessions_When_IssuerIdentityOrDestinationIsSubstituted_Then_OnlyOriginalConfirmationCanRevokeItsSource(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var first = await BrowserSession.CreateAsync();
            var page = await first.Context.NewPageAsync();
            var source = await BrowserLogoutJourney.BeginAsync(environment, page);
            var link = await BrowserLogoutJourney.LinkAsync(environment, page);
            var original = await CookieAsync(page);
            await using var second = await BrowserSession.CreateAsync();
            var other = await second.Context.NewPageAsync();
            await BrowserHttpsJourney.SignInAsync(environment, other);
            var otherSource = await BrowserHttpsState.CurrentSourceAsync(environment);
            Assert.True(source != otherSource);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            await page.Context.ClearCookiesAsync(new()
            {
                Name = BrowserLogoutJourney.IssuerCookie
            });
            await BrowserLogoutJourney.DeniedAsync(page, link);
            await page.Context.AddCookiesAsync([await CookieAsync(other)]);
            await BrowserLogoutJourney.DeniedAsync(page, link);
            await page.Context.AddCookiesAsync([original]);
            await DenyFieldsAsync(page, link, otherSource);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
            await DenyConfirmationAsync(environment, page, other, link, source, before, original);
            await BrowserLogoutJourney.ConfirmAsync(environment, page);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: true);
            await BrowserHttpsState.ReturnedAsync(environment, source);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            await BrowserHttpsJourney.RequireStatusAsync(environment, other, true);
            Assert.True(await BrowserHttpsState.CurrentSourceAsync(environment) == otherSource);
            Assert.Equal(7, environment.EndSessionGets);
            Assert.Equal(2, environment.EndSessionPosts);
            environment.RequirePolicies(ClientCounts, IssuerCounts);
        });

    /// <summary>A genuine confirmation form cannot authorize another live issuer session substituted immediately before its POST.</summary>
    private static async Task DenyConfirmationAsync(BrowserHttpsFixture environment, IPage page, IPage other, Uri link, string source, string before, Cookie original)
    {
        await page.GotoAsync(link.AbsoluteUri);
        await page.Context.AddCookiesAsync([await CookieAsync(other)]);
        var denied = await page.RunAndWaitForResponseAsync(() => page.GetByRole(AriaRole.Button,
            new()
            {
                Name = BrowserLogoutJourney.Confirm,
                Exact = true
            }).ClickAsync(),
            response => response.Request.Method == Microsoft.AspNetCore.Http.HttpMethods.Post);
        Assert.Equal(Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest, denied.Status);
        await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
        Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
        await page.Context.AddCookiesAsync([original]);
        await page.GotoAsync(link.AbsoluteUri);
    }

    /// <summary>Changes exactly one native field while preserving the original product link and real browser cookie.</summary>
    private static async Task DenyFieldsAsync(IPage page, Uri link, string otherSource)
    {
        foreach (var change in new[] { (Client, ForeignClient), (Hint, otherSource), (Return, ForeignReturn) })
        {
            var fields = QueryHelpers.ParseQuery(link.Query).ToDictionary(item => item.Key, item => (string?)item.Value.ToString(), StringComparer.Ordinal);
            fields[change.Item1] = change.Item2;
            var destination = new Uri(QueryHelpers.AddQueryString(link.GetLeftPart(UriPartial.Path), fields));
            await BrowserLogoutJourney.DeniedAsync(page, destination);
            Assert.True(Uri.TryCreate(page.Url, UriKind.Absolute, out var actual) && actual.GetLeftPart(UriPartial.Path) == link.GetLeftPart(UriPartial.Path));
        }
    }

    /// <summary>Copies only an actual issuer cookie obtained from a completed native browser ceremony, never creating authority.</summary>
    private static async Task<Cookie> CookieAsync(IPage page)
    {
        var cookies = (await page.Context.CookiesAsync()).Where(cookie => cookie.Name == BrowserLogoutJourney.IssuerCookie).ToArray();
        var count = cookies.Length;
        Assert.Equal(1, count);
        var cookie = cookies[0];
        return new Cookie
        {
            Name = cookie.Name,
            Value = cookie.Value,
            Domain = cookie.Domain,
            Path = cookie.Path,
            Secure = cookie.Secure,
            HttpOnly = cookie.HttpOnly,
            SameSite = cookie.SameSite,
            Expires = cookie.Expires
        };
    }
}
