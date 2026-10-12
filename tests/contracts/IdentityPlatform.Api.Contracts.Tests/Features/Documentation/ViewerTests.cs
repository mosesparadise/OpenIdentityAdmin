using System.Collections.Concurrent;
using Deque.AxeCore.Playwright;
using IdentityPlatform.Api.Contracts.Tests.Fixtures;
using Microsoft.Playwright;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Documentation;

[Trait("Category", "Browser")]
public sealed class ViewerTests
{
    [Theory]
    [InlineData("scalar", false)]
    [InlineData("scalar", true)]
    [InlineData("swagger", false)]
    [InlineData("swagger", true)]
    public async Task Both_viewers_render_local_read_only_contracts_without_credentials(string viewer, bool dark)
    {
        await using var host = await ContractHost.StartAsync(browserSurface: true, realHttps: true);
        await using var browser = await BrowserSession.CreateAsync();
        var page = await browser.Context.NewPageAsync();
        await page.EmulateMediaAsync(new()
        {
            ColorScheme = dark ? ColorScheme.Dark : ColorScheme.Light
        });
        var external = new ConcurrentQueue<string>();
        var errors = new ConcurrentQueue<string>();
        var mutations = new ConcurrentQueue<string>();
        var origin = host.Client.BaseAddress!.GetLeftPart(UriPartial.Authority);
        page.Console += (_, message) => { if (message.Type == "error") { errors.Enqueue(message.Text); } };
        await page.RouteAsync("**/*", async route =>
        {
            var requestUri = new Uri(route.Request.Url);
            if (requestUri.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal) || requestUri.AbsolutePath.StartsWith("/bff/api/", StringComparison.Ordinal))
            {
                mutations.Enqueue(route.Request.Method);
                await route.AbortAsync();
                return;
            }
            if (requestUri.GetLeftPart(UriPartial.Authority) != origin)
            {
                external.Enqueue(requestUri.Host);
                await route.AbortAsync();
            }
            else
            {
                await route.ContinueAsync();
            }
        });
        var response = await page.GotoAsync(origin + "/docs/" + viewer + "/");
        Assert.Equal(200, response!.Status);
        await Assertions.Expect(page.GetByText("List synthetic examples", new()
        {
            Exact = true
        }).First).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Update a synthetic example", new()
        {
            Exact = true
        }).First).ToBeVisibleAsync();
        Assert.DoesNotContain("unsafe-eval", response.Headers["content-security-policy"], StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers["cache-control"]);
        Assert.False(await page.EvaluateAsync<bool>("Boolean(window.fixtureInjected)"));
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new()
        {
            NameRegex = new("^(Authorize|Try it out|Execute|Test Request)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        }).CountAsync());
        if (viewer == "swagger")
        {
            await page.GetByLabel("Contract", new()
            {
                Exact = true
            }).SelectOptionAsync("v1");
        }
        else
        {
            await page.GotoAsync(origin + "/docs/scalar/v1");
        }
        await Assertions.Expect(page.GetByText("IdentityPlatform Admin API", new()
        {
            Exact = false
        }).First).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("List synthetic examples", new()
        {
            Exact = true
        }).First).ToBeVisibleAsync();
        Assert.Equal(0, await page.Locator("input[type=password]").CountAsync());
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new()
        {
            NameRegex = new("^(Authorize|Try it out|Execute|Test Request)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        }).CountAsync());
        Assert.Empty(mutations);
        Assert.Empty(external);
        Assert.True(errors.IsEmpty, string.Join(Environment.NewLine, errors));
        Assert.Equal(0, await page.EvaluateAsync<int>("Object.keys(localStorage).filter(k => /token|auth|credential/i.test(k)).length"));
        var axe = await page.RunAxe();
        Assert.True(axe.Violations.Length == 0, System.Text.Json.JsonSerializer.Serialize(axe.Violations));
        await page.SetViewportSizeAsync(320, 900);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
    }

    [Fact]
    public async Task Generated_consumer_uses_real_browser_cookies_and_explicit_request_proof()
    {
        await using var host = await ContractHost.StartAsync(browserSurface: true, realHttps: true);
        await using var browser = await BrowserSession.CreateAsync();
        var page = await browser.Context.NewPageAsync();
        var origin = host.Client.BaseAddress!.GetLeftPart(UriPartial.Authority);
        await page.GotoAsync(origin + "/consumer");
        Assert.Equal("fixture", await page.EvaluateAsync<string>("async () => (await contractConsumer.run()).id"));
        Assert.Equal(403, await page.EvaluateAsync<int>("async () => (await fetch('/bff/api/v1/examples/fixture', { method:'PUT', headers:{'Content-Type':'application/json','If-Match':'\"fixture-v1\"'}, body:JSON.stringify({label:'Synthetic'}) })).status"));
        Assert.Equal(428, await page.EvaluateAsync<int>("async () => (await fetch('/bff/api/v1/examples/fixture', { method:'PUT', headers:{'Content-Type':'application/json','X-CSRF-TOKEN':'synthetic-proof'}, body:JSON.stringify({label:'Synthetic'}) })).status"));
        Assert.Equal("", await page.EvaluateAsync<string>("document.cookie"));
        Assert.Equal(0, await page.EvaluateAsync<int>("localStorage.length + sessionStorage.length"));
        await browser.Context.ClearCookiesAsync();
        Assert.Equal(401, await page.EvaluateAsync<int>("async () => (await fetch('/bff/api/v1/examples')).status"));
    }
}
