using System.Collections.Concurrent;
using Deque.AxeCore.Playwright;
using IdentityPlatform.Api.Contracts.Tests.Fixtures;
using Microsoft.Playwright;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Documentation;

/// <summary>Verifies both local read-only documentation viewers and the real browser consumer boundary.</summary>
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


    }

    private const string BrowserTitle = "IdentityPlatform Admin BFF browser API";
    private const string ApiTitle = "IdentityPlatform Admin API";
    private const string ScalarViewer = "scalar";
    private const string BodySelector = "body";
    private const string BrowserExamplePath = "/bff/api/v1/examples";
    private const string ApiExamplePath = "/api/v1/examples";
    private const string ProblemModel = "ProblemDetails";
    private const string ExampleModel = "Example";
    private const string NoViolations = "[]";
    private const string ReadCsp = "JSON.stringify(window.contractCsp)";
    private const string InitialStage = "initial";
    private const string RenderedStage = "rendered";
    private const string NarrowStage = "narrow";
    private const int NarrowWidth = 320;
    private const int WideWidth = 1280;
    private const int ViewportHeight = 900;
    private static readonly string[] OperationSummaries = ["List synthetic examples", "Get a synthetic example", "Update a synthetic example"];
    private const string WaitForPaint = "() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))";
    private const string ReadOverflow = "JSON.stringify({viewport:innerWidth, root:document.documentElement.scrollWidth, body:document.body.scrollWidth, elements:[...document.querySelectorAll('body *')].filter(e => e.getBoundingClientRect().right > innerWidth + 1 || e.scrollWidth > e.clientWidth + 1).slice(0,20).map(e => ({tag:e.tagName, class:String(e.className), width:e.getBoundingClientRect().width, right:e.getBoundingClientRect().right, scroll:e.scrollWidth, client:e.clientWidth, overflow:getComputedStyle(e).overflowX}))})";
    private const string NoOverflow = "document.documentElement.scrollWidth <= innerWidth + 1";

    /// <summary>Requires the same contracts, native keyboard behavior, CSP and accessible layouts at both viewport widths.</summary>
    private static async Task VerifyRenderedDocumentAsync(IPage page, string viewer, bool dark, bool browserSurface)
    {
        var title = browserSurface ? BrowserTitle : ApiTitle;
        await Assertions.Expect(page.GetByText(title, new()
        {
            Exact = false
        }).First).ToBeVisibleAsync();
        foreach (var summary in OperationSummaries)
        {
            await Assertions.Expect(page.GetByText(summary, new()
            {
                Exact = true
            }).First).ToBeVisibleAsync();
        }
        await CaptureAsync(page, viewer, dark, browserSurface, InitialStage);
        if (viewer == ScalarViewer)
        {
            await ScalarNavigation.RequireAsync(page);
            await ScalarModels.RequireAsync(page);
        }
        var body = await page.Locator(BodySelector).InnerTextAsync();
        Assert.Contains(browserSurface ? BrowserExamplePath : ApiExamplePath, body, StringComparison.Ordinal);
        Assert.Contains(ProblemModel, body, StringComparison.Ordinal);
        Assert.Contains(ExampleModel, body, StringComparison.Ordinal);
        await CaptureAsync(page, viewer, dark, browserSurface, RenderedStage);
        Assert.Equal(NoViolations, await page.EvaluateAsync<string>(ReadCsp));
        await AssertAccessibleAsync(page);
        await page.SetViewportSizeAsync(NarrowWidth, ViewportHeight);
        // Resize returns before every engine has painted its responsive layout.
        await page.EvaluateAsync(WaitForPaint);
        await CaptureAsync(page, viewer, dark, browserSurface, NarrowStage);
        var overflow = await page.EvaluateAsync<string>(ReadOverflow);
        Assert.True(await page.EvaluateAsync<bool>(NoOverflow), overflow);
        await AssertAccessibleAsync(page);
        await page.SetViewportSizeAsync(WideWidth, ViewportHeight);

    }

    private static async Task AssertAccessibleAsync(IPage page)
    {
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
