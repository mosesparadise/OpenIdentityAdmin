using Deque.AxeCore.Commons;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

[Collection("issuer")]
[Trait("Category", "Browser")]
public sealed class BrowserEvidenceTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Sensitive_browser_and_timeout_diagnostics_are_suppressed_but_assertions_remain_failures(string provider)
    {
        Assert.True(provider is "PostgreSql" or "SqlServer");
        var sentinel = "synthetic-private-action-canary-" + Guid.NewGuid().ToString("N");
        foreach (var original in new Exception[] { new PlaywrightException(sentinel), new TimeoutException(sentinel) })
        {
            var safe = await Record.ExceptionAsync(() => BrowserEvidence.RunAsync(() => Task.FromException(original)));
            Assert.True(safe is Xunit.Sdk.XunitException, "Browser failure sanitization did not preserve a safe test failure.");
            Assert.True(!safe!.ToString().Contains(sentinel, StringComparison.Ordinal), "Sensitive browser diagnostic reached test evidence.");
            Assert.True(safe.InnerException is null, "Original browser diagnostics must not survive as an inner exception.");
        }
        var callbackFailure = BrowserJourneys.CallbackFailure("https://example.invalid/" + sentinel + "?value=" + sentinel,
            BrowserJourneyRole.SecondTab, 400, 0, 0, false);
        Assert.True(callbackFailure.Message.Contains("SecondTab at other", StringComparison.Ordinal) &&
            !callbackFailure.ToString().Contains(sentinel, StringComparison.Ordinal) && callbackFailure.InnerException is null,
            "Callback diagnostics must contain only fixed categories and counters.");
        var phased = await Record.ExceptionAsync(() => BrowserEvidence.RunAsync(async () =>
        {
            await Task.Yield();
            BrowserEvidence.Mark(BrowserPhase.TrustedIssuer);
            throw new PlaywrightException(sentinel);
        }));
        Assert.True(phased is Xunit.Sdk.XunitException && phased.Message.Contains("TrustedIssuer", StringComparison.Ordinal) &&
            !phased.ToString().Contains(sentinel, StringComparison.Ordinal), "Async browser phase diagnostics must remain accurate and secret-safe.");
        var axeEvidence = BrowserAccessibility.Describe(new AxeResult(JObject.FromObject(new
        {
            violations = new[] { new { id = "color-contrast", nodes = new[] { new
            {
                html = sentinel, target = new[] { "#submit", sentinel },
                any = new[] { new { data = new { fgColor = "#ffffff", bgColor = sentinel, contrastRatio = 2.5, message = sentinel } } }
            } } } }
        })));
        Assert.True(axeEvidence.Contains("color-contrast: #submit/other element", StringComparison.Ordinal) &&
            axeEvidence.Contains("foreground=#ffffff, background=unknown, contrast=2.5", StringComparison.Ordinal) &&
            !axeEvidence.Contains(sentinel, StringComparison.Ordinal), "Accessibility diagnostics must retain only fixed targets and numeric/color facts.");
        var styleEvidence = BrowserAccessibility.DescribeStyles(System.Text.Json.JsonSerializer.SerializeToElement(new[]
        {
            new { index = 0, color = "#ffffff", fill = sentinel, background = "#000000", focus = true, hover = false, top = 12, height = 48, scroll = 0, text = sentinel }
        }));
        Assert.True(styleEvidence.Contains("#submit: color=#ffffff, fill=unknown, background=#000000", StringComparison.Ordinal) &&
            !styleEvidence.Contains(sentinel, StringComparison.Ordinal), "Computed-style diagnostics must exclude arbitrary values.");
        var assertion = new Xunit.Sdk.XunitException("Expected invariant failure");
        var preserved = await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(() => BrowserEvidence.RunAsync(() => Task.FromException(assertion)));
        Assert.Same(assertion, preserved);
        Assert.Throws<InvalidOperationException>(() => BrowserSession.SelectEngine("unsupported"));
        Assert.Equal("chromium", BrowserSession.SelectEngine(null));
        Assert.Equal("firefox", BrowserSession.SelectEngine("firefox"));
        Assert.Equal("webkit", BrowserSession.SelectEngine("webkit"));
        await BrowserEvidence.RunAsync(async () =>
        {
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await page.SetContentAsync("<!doctype html><html lang='en'><head><title>Contrast control</title></head><body><main><p id='contrast-probe' style='color:#aaaaaa;background:#ffffff'>Synthetic contrast control</p></main></body></html>");
            var contrast = await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(() => BrowserAccessibility.RequireNoViolationsAsync(page));
            Assert.True(contrast.Message.Contains("color-contrast: #contrast-probe", StringComparison.Ordinal) &&
                contrast.Message.Contains("foreground=#aaaaaa, background=#ffffff", StringComparison.Ordinal),
                "The actual axe scan must detect the seeded contrast fault and preserve its safe color evidence.");
            await page.Locator("#contrast-probe").EvaluateAsync("element => element.style.color = '#000000'");
            await BrowserAccessibility.RequireNoViolationsAsync(page);
        });
    }
}
