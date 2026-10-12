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
        await page.AddInitScriptAsync("window.contractCsp = []; document.addEventListener('securitypolicyviolation', e => window.contractCsp.push({ directive:e.effectiveDirective, blocked:e.blockedURI }));");
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
        await VerifyRenderedDocumentAsync(page, viewer, dark, browserSurface: true);
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
            await page.GetByRole(AriaRole.Button, new()
            {
                Name = "Browser BFF v1",
                Exact = true
            }).ClickAsync();
            await page.GetByRole(AriaRole.Option, new()
            {
                Name = "Downstream Admin API v1",
                Exact = true
            }).ClickAsync();
        }
        await Assertions.Expect(page.GetByText("IdentityPlatform Admin API", new()
        {
            Exact = false
        }).First).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("List synthetic examples", new()
        {
            Exact = true
        }).First).ToBeVisibleAsync();
        await VerifyRenderedDocumentAsync(page, viewer, dark, browserSurface: false);
        Assert.Equal(0, await page.Locator("input[type=password]").CountAsync());
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new()
        {
            NameRegex = new("^(Authorize|Try it out|Execute|Test Request)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        }).CountAsync());
        Assert.Empty(mutations);
        Assert.Empty(external);
        Assert.True(errors.IsEmpty, string.Join(Environment.NewLine, errors));
        Assert.Equal(0, await page.EvaluateAsync<int>("Object.keys(localStorage).filter(k => /token|auth|credential/i.test(k)).length"));

        await page.SetViewportSizeAsync(320, 900);
        var overflow = await page.EvaluateAsync<string>("JSON.stringify([...document.querySelectorAll('body *')].filter(e => e.getBoundingClientRect().right > innerWidth + 1 && getComputedStyle(e).position !== 'fixed').slice(0,12).map(e => ({tag:e.tagName, class:e.className, width:e.getBoundingClientRect().width})))");
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"), overflow);
    }

    private static async Task VerifyRenderedDocumentAsync(IPage page, string viewer, bool dark, bool browserSurface)
    {
        var title = browserSurface ? "IdentityPlatform Admin BFF browser API" : "IdentityPlatform Admin API";
        await Assertions.Expect(page.GetByText(title, new()
        {
            Exact = false
        }).First).ToBeVisibleAsync();
        foreach (var summary in new[] { "List synthetic examples", "Get a synthetic example", "Update a synthetic example" })
        {
            await Assertions.Expect(page.GetByText(summary, new()
            {
                Exact = true
            }).First).ToBeVisibleAsync();
        }
        await CaptureAsync(page, viewer, dark, browserSurface, "initial");
        if (viewer == "scalar")
        {
            await page.Locator(".t-doc__sidebar").GetByText("Models", new()
            {
                Exact = true
            }).ClickAsync();
            var models = page.GetByRole(AriaRole.Region, new()
            {
                Name = "Models",
                Exact = true
            });
            await models.ScrollIntoViewIfNeededAsync();
            var more = models.GetByRole(AriaRole.Button, new()
            {
                Name = "Show more",
                Exact = true
            });
            if (await more.IsVisibleAsync())
            {
                await more.ClickAsync();
            }
            await page.Locator(".t-doc__sidebar").GetByText("ProblemDetails", new()
            {
                Exact = true
            }).ClickAsync();
            var problem = page.GetByRole(AriaRole.Region, new()
            {
                Name = "ProblemDetails",
                Exact = true
            });
            await problem.ScrollIntoViewIfNeededAsync();
            var expand = problem.Locator("button[aria-expanded=false]").First;
            if (await expand.IsVisibleAsync())
            {
                await expand.ClickAsync();
            }
            await Assertions.Expect(problem.GetByText("status", new()
            {
                Exact = true
            }).First).ToBeVisibleAsync();
        }
        var body = await page.Locator("body").InnerTextAsync();
        Assert.Contains(browserSurface ? "/bff/api/v1/examples" : "/api/v1/examples", body, StringComparison.Ordinal);
        Assert.Contains("ProblemDetails", body, StringComparison.Ordinal);
        Assert.Contains("Example", body, StringComparison.Ordinal);
        await CaptureAsync(page, viewer, dark, browserSurface, "rendered");
        Assert.Equal("[]", await page.EvaluateAsync<string>("JSON.stringify(window.contractCsp)"));
        var axe = await page.RunAxe();
        Assert.True(axe.Violations.Length == 0, System.Text.Json.JsonSerializer.Serialize(axe.Violations.Select(v => new { v.Id, Nodes = v.Nodes.Select(n => new { n.Html, Messages = n.Any.Select(a => a.Message) }) })));
    }

    private static async Task CaptureAsync(IPage page, string viewer, bool dark, bool browserSurface, string stage)
    {
        var output = Environment.GetEnvironmentVariable("IDENTITY_CONTRACT_OUTPUT");
        if (output is not null)
        {
            var directory = Path.Combine(output, "viewers", Environment.GetEnvironmentVariable("IDENTITY_TEST_BROWSER") ?? "chromium");
            Directory.CreateDirectory(directory);
            var name = $"{viewer}-{(dark ? "dark" : "light")}-{(browserSurface ? "bff" : "api")}-{stage}";
            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(directory, name + ".png")
            });
            await File.WriteAllTextAsync(Path.Combine(directory, name + ".html"), await page.ContentAsync(), TestContext.Current.CancellationToken);
        }
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
