using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using IdentityPlatform.Api.Contracts.Tests.Fixtures;
using Microsoft.Playwright;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Configuration;

/// <summary>Verifies both pinned viewers treat hostile configuration and actually served document markup as inert text.</summary>
[Trait("Category", "Browser")]
public sealed class ConfiguredViewerTests
{
    private const string Scalar = "scalar";
    private const string DocumentationRoot = "/docs/";
    private const string BffDocument = "/openapi/bff-v1.json";
    private const string ApiDocument = "/openapi/v1.json";
    private const string InfoKey = "info";
    private const string DescriptionKey = "description";
    private const string RequestPattern = "**/*";
    private const string ContractLabel = "Contract";
    private const string ApiSelector = "v1";
    private const string OptionsSelector = "option";
    private const string UpstreamTitle = "IdentityPlatform Admin API";
    private const string FixtureDescription = "NON-SHIPPED tooling fixture.";
    private const string UnsafeImageHandlers = "img[onload], img[onerror]";
    private const string InjectionState = "Boolean(window.fixtureInjected || window.settingsInjected)";

    /// <summary>Assert the attack inputs exist, exercise both document selections and require literal labels without script execution or external requests.</summary>
    [Theory]
    [InlineData("scalar")]
    [InlineData("swagger")]
    public async Task Given_HostileSettingsAndDocument_When_EachViewerRenders_Then_TextIsLiteralAndScriptsStayInert(string viewer)
    {
        await using var host = await ContractHost.StartAsync(true, realHttps: true, ViewerConfigurationTests.HostileSettings());
        await RequireProbeAsync(host, BffDocument);
        await RequireProbeAsync(host, ApiDocument);
        await using var browser = await BrowserSession.CreateAsync();
        var page = await browser.Context.NewPageAsync();
        var external = new ConcurrentQueue<string>();
        var origin = host.Client.BaseAddress!.GetLeftPart(UriPartial.Authority);
        await ObserveRequestsAsync(page, origin, external);
        await page.GotoAsync(origin + DocumentationRoot + viewer);
        await Assertions.Expect(page).ToHaveTitleAsync(ViewerConfigurationTests.HostileTitle);
        await Assertions.Expect(page.GetByText(FixtureDescription, new()
        {
            Exact = false
        }).First).ToBeVisibleAsync();
        await RequireInertAsync(page);
        await SelectUpstreamAsync(page, viewer);
        await Assertions.Expect(page.GetByText(UpstreamTitle, new()
        {
            Exact = false
        }).First).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText(FixtureDescription, new()
        {
            Exact = false
        }).First).ToBeVisibleAsync();
        await RequireInertAsync(page);
        Assert.Empty(external);
    }

    /// <summary>Prove handler removal independently of CSP blocking script execution.</summary>
    private static async Task RequireInertAsync(IPage page)
    {
        const int NoHandlers = 0;
        Assert.Equal(NoHandlers, await page.Locator(UnsafeImageHandlers).CountAsync());
        Assert.False(await page.EvaluateAsync<bool>(InjectionState));
    }

    /// <summary>Assert against the actual served JSON, preventing a later transformer from silently replacing the attack fixture.</summary>
    private static async Task RequireProbeAsync(ContractHost host, string path)
    {
        const string ExpectedProbe = "NON-SHIPPED tooling fixture. <img alt=\"\" src=\"data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7\" onload=window.fixtureInjected=true>";
        var document = JsonNode.Parse(await host.Client.GetStringAsync(path, TestContext.Current.CancellationToken))!;
        Assert.Equal(ExpectedProbe, document[InfoKey]![DescriptionKey]!.GetValue<string>());
    }

    /// <summary>Observe and block external traffic while allowing only the configured local host during the security regression.</summary>
    private static Task<IAsyncDisposable> ObserveRequestsAsync(IPage page, string origin, ConcurrentQueue<string> external) => page.RouteAsync(RequestPattern, async route =>
    {
        if (new Uri(route.Request.Url).GetLeftPart(UriPartial.Authority) != origin)
        {
            external.Enqueue(route.Request.Url);
            await route.AbortAsync();
        }
        else
        {
            await route.ContinueAsync();
        }
    });

    /// <summary>Verify both configured document labels retain their literal text through the native viewer controls.</summary>
    private static async Task SelectUpstreamAsync(IPage page, string viewer)
    {
        if (viewer == Scalar)
        {
            await page.GetByRole(AriaRole.Button, new()
            {
                Name = ViewerConfigurationTests.BrowserLabel,
                Exact = true
            }).ClickAsync();
            await page.GetByRole(AriaRole.Option, new()
            {
                Name = ViewerConfigurationTests.AdminLabel,
                Exact = true
            }).ClickAsync();
        }
        else
        {
            var selector = page.GetByLabel(ContractLabel, new()
            {
                Exact = true
            });
            await Assertions.Expect(selector.Locator(OptionsSelector)).ToHaveTextAsync([ViewerConfigurationTests.BrowserLabel, ViewerConfigurationTests.AdminLabel]);
            await selector.SelectOptionAsync(ApiSelector);
        }
    }
}
