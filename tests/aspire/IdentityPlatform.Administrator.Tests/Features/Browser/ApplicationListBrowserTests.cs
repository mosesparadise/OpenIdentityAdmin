using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises the actual protected BFF/API collection after native password and MFA on each relational provider.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationListBrowserTests(BootstrapFixture fixture)
{
    private const int Success = 200;
    private const int DefaultPageSize = 25;
    private const string SignedIn = "You’re signed in";
    private const string MissingPrefix = "no-such-%_[]\\";
    private const string Search = "Search";
    private const string ExpiresAt = "expiresAt";
    private const string Visibility = "() => document.dispatchEvent(new Event('visibilitychange'))";
    private const string Rows = "tbody tr";
    private const string Reload = "Reload applications";
    private const string Page = "page";
    private const string PageSize = "pageSize";
    private const string Items = "items";
    private const string ClientId = "clientId";
    private const string Identifier = "id";
    private const string RegisteredClient = "identity-admin-bff";
    private static readonly string[] PageFields = [ExpiresAt, "hasNextPage", Items, Page, PageSize];
    private static readonly string[] ItemFields = [ClientId, "clientType", "displayName", Identifier];
    private const string ReadCollection = "async () => { const response = await fetch('/bff/api/v1/applications', {cache:'no-store', credentials:'same-origin', redirect:'error'}); if (response.status !== 200) throw new Error('Application collection rejected'); return await response.text(); }";
    private const string Applications = "/applications";
    private const string Collection = "/bff/api/v1/applications";
    private const string SearchLabel = "Client ID starts with";
    private const string Registered = "Registered applications";
    private const string Empty = "No matching applications";
    private const string Expired = "Sign in to view applications";

    /// <summary>Anonymous denial, native sign-in, exact safe JSON, literal prefix search and original-token expiry use real trusted HTTPS hosts.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RegisteredApplications_When_AdministratorSignsInAndSearches_Then_RealCollectionExpiresWithoutRenewal(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await page.Clock.SetFixedTimeAsync(environment.Issuer.Account.Journey.Clock.Now.UtcDateTime);
            await page.GotoAsync(new Uri(environment.Profile.ClientUri, Applications).AbsoluteUri);
            await HeadingAsync(page, Expired);
            await BrowserHttpsJourney.BeginAsync(environment, page);
            await BrowserHttpsJourney.PasswordAsync(environment, page);
            await BrowserHttpsJourney.FactorAsync(environment, page);
            await HeadingAsync(page, SignedIn);
            var response = await page.RunAndWaitForResponseAsync(() => page.GotoAsync(new Uri(environment.Profile.ClientUri, Applications).AbsoluteUri),
                result => result.Url.Contains(Collection, StringComparison.Ordinal));
            Assert.Equal(Success, response.Status);
            await HeadingAsync(page, Registered);
            using var body = JsonDocument.Parse(await page.EvaluateAsync<string>(ReadCollection));
            RequireProjection(body.RootElement);
            await WorkspacePresentation.RequireStorageAsync(page);
            await WorkspacePresentation.RequireStateAsync(page);
            await RequireSearchAsync(page);
            var expiry = body.RootElement.GetProperty(ExpiresAt).GetDateTimeOffset();
            await page.Clock.SetFixedTimeAsync(expiry.UtcDateTime);
            await page.EvaluateAsync(Visibility);
            await HeadingAsync(page, Expired);
            Assert.Equal(0, await page.Locator(Rows).CountAsync());
            environment.Issuer.Account.Journey.Clock.Now = expiry;
            await page.GetByRole(AriaRole.Button, new()
            {
                Name = Reload,
                Exact = true
            }).ClickAsync();
            await HeadingAsync(page, Expired);
            Assert.Equal(1, environment.TokenRequests);
        });

    /// <summary>Checks a literal empty search and restores the current protected collection.</summary>
    private static async Task RequireSearchAsync(IPage page)
    {
        await page.GetByLabel(SearchLabel, new()
        {
            Exact = true
        }).FillAsync(MissingPrefix);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = Search,
            Exact = true
        }).ClickAsync();
        await HeadingAsync(page, Empty);
        await page.GetByLabel(SearchLabel, new()
        {
            Exact = true
        }).FillAsync(string.Empty);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = Search,
            Exact = true
        }).ClickAsync();
        await HeadingAsync(page, Registered);
    }

    /// <summary>Requires exactly the approved summary fields without secrets, permissions, redirect URIs or native serialized entities.</summary>
    private static void RequireProjection(JsonElement document)
    {
        Assert.Equal(PageFields, document.EnumerateObject().Select(item => item.Name).Order().ToArray());
        Assert.Equal(1, document.GetProperty(Page).GetInt32());
        Assert.Equal(DefaultPageSize, document.GetProperty(PageSize).GetInt32());
        var items = document.GetProperty(Items).EnumerateArray().ToArray();
        Assert.Contains(items, item => item.GetProperty(ClientId).GetString() == RegisteredClient);
        foreach (var item in items)
        {
            Assert.Equal(ItemFields, item.EnumerateObject().Select(field => field.Name).Order().ToArray());
            Assert.False(string.IsNullOrEmpty(item.GetProperty(Identifier).GetString()));
        }
    }

    /// <summary>Uses visible accessible state names without exposing protected raw browser payloads in failure output.</summary>
    private static Task HeadingAsync(IPage page, string text) => page.GetByRole(AriaRole.Heading, new() { Name = text, Exact = true }).WaitForAsync();
}
