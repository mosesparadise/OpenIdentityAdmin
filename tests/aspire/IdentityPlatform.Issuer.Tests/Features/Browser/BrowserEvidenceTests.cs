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
            BrowserJourneyRole.Recovery, 302, 1, 1, true, false, true);
        Assert.True(callbackFailure.Message.Contains("Recovery at other", StringComparison.Ordinal) &&
            callbackFailure.Message.Contains("completion True, validated result False, exact callback True", StringComparison.Ordinal) &&
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
        foreach (var (target, expected) in new (object Target, string Expected)[]
        {
            (new[] { "#submit" }, "#submit"),
            (new[] { "#submit", sentinel }, "#submit/other element"),
            (new[] { sentinel }, "other element")
        })
        {
            var axeEvidence = BrowserAccessibility.Describe(new AxeResult(JObject.FromObject(new
            {
                violations = new[] { new { id = "color-contrast", nodes = new[] { new
                {
                    html = sentinel, target,
                    any = new[] { new { data = new { fgColor = "#ffffff", bgColor = sentinel, contrastRatio = 2.5, message = sentinel } } }
                } } } }
            })));
            Assert.True(axeEvidence.Contains("color-contrast: " + expected + " (", StringComparison.Ordinal) &&
                axeEvidence.Contains("foreground=#ffffff, background=unknown, contrast=2.5", StringComparison.Ordinal) &&
                !axeEvidence.Contains(sentinel, StringComparison.Ordinal), "Accessibility diagnostics must handle scalar/chained selectors without retaining arbitrary data.");
        }
        var styleEvidence = BrowserAccessibility.DescribeStyles(System.Text.Json.JsonSerializer.SerializeToElement(new[]
        {
            new { index = 0, color = "#ffffff", fill = sentinel, background = "#000000", focus = true, hover = false, top = 12, height = 48, scroll = 0, text = sentinel }
        }));
        Assert.True(styleEvidence.Contains("#submit: color=#ffffff, fill=unknown, background=#000000", StringComparison.Ordinal) &&
            !styleEvidence.Contains(sentinel, StringComparison.Ordinal), "Computed-style diagnostics must exclude arbitrary values.");
        var canvasEvidence = BrowserStyleEvidence.Describe(System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            bodyBackgroundKind = "transparent",
            rootBackgroundKind = "unsupported",
            pageToken = "#020617",
            textToken = sentinel,
            bodyScheme = sentinel,
            stylesheet = "unavailable",
            bodyRule = sentinel,
            sheetDisabled = 1,
            inlineOverride = false,
            ready = sentinel,
            theme = sentinel,
            bodyConnected = true,
            cssText = sentinel,
            url = sentinel
        }));
        Assert.True(canvasEvidence.Contains("body-background-kind=transparent, root-background-kind=unsupported, page-token=#020617, text-token=unknown", StringComparison.Ordinal) &&
            canvasEvidence.Contains("stylesheet=unavailable, sheet-disabled=unknown, body-rule=unknown", StringComparison.Ordinal) &&
            canvasEvidence.Contains("inline-override=false", StringComparison.Ordinal) && !canvasEvidence.Contains(sentinel, StringComparison.Ordinal),
            "Canvas diagnostics must distinguish unknown values and unavailable CSSOM without retaining arbitrary data.");
        Assert.Equal("; canvas evidence unavailable", BrowserStyleEvidence.Describe(System.Text.Json.JsonSerializer.SerializeToElement(sentinel)));
        foreach (var malformed in new object[] { sentinel, new { }, new { styles = sentinel, canvas = sentinel }, new { styles = Array.Empty<object>() } })
        {
            Assert.Equal("; style snapshot unavailable", BrowserAccessibility.DescribeSnapshot(System.Text.Json.JsonSerializer.SerializeToElement(malformed)));
        }
        var malformedNodes = BrowserAccessibility.DescribeSnapshot(System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            styles = new object?[] { null, sentinel, new { index = sentinel } },
            canvas = sentinel
        }));
        Assert.Equal("; computed styles: ; canvas evidence unavailable", malformedNodes);
        var assertion = new Xunit.Sdk.XunitException("Expected invariant failure");
        var preserved = await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(() => BrowserEvidence.RunAsync(() => Task.FromException(assertion)));
        Assert.Same(assertion, preserved);
        Assert.Throws<InvalidOperationException>(() => BrowserSession.SelectEngine("unsupported"));
        Assert.Equal("chromium", BrowserSession.SelectEngine(null));
        Assert.Equal("firefox", BrowserSession.SelectEngine("firefox"));
        Assert.Equal("webkit", BrowserSession.SelectEngine("webkit"));
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Callback_document_requires_exact_flow_and_full_readiness_even_when_navigation_already_completed(string provider)
    {
        Assert.True(provider is "PostgreSql" or "SqlServer");
        await BrowserEvidence.RunAsync(async () =>
        {
            await using var session = await BrowserSession.CreateAsync(javaScript: false);
            var page = await session.Context.NewPageAsync();
            const string callback = "https://callback.example.invalid/callback";
            const string state = "synthetic-callback-control";
            var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync("https://callback.example.invalid/**", async route =>
            {
                if (new Uri(route.Request.Url).AbsolutePath == "/pending")
                {
                    requested.TrySetResult();
                    await release.Task;
                    await route.FulfillAsync(new()
                    {
                        ContentType = "image/svg+xml",
                        Body = "<svg xmlns='http://www.w3.org/2000/svg' width='1' height='1'/>"
                    });
                }
                else
                {
                    await route.FulfillAsync(new()
                    {
                        ContentType = "text/html",
                        Body = "<!doctype html><html lang='en'><title>Callback control</title><body><img src='/pending' alt=''>Control</body></html>"
                    });
                }
            });
            try
            {
                BrowserEvidence.Mark(BrowserPhase.InitialNavigation);
                await page.GotoAsync(callback + "?state=" + state, new()
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded
                });
                await requested.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Assert.True(await page.EvaluateAsync<string>("document.readyState") != "complete", "The control must hold a real document subresource open.");
                await Assert.ThrowsAsync<TimeoutException>(() => BrowserJourneys.WaitForCallbackDocumentAsync(page, callback, state, 250));
                release.TrySetResult();
                BrowserEvidence.Mark(BrowserPhase.Callback);
                await BrowserJourneys.WaitForCallbackDocumentAsync(page, callback, state);
                // Invoke after completion again: no future navigation/load event is required.
                await BrowserJourneys.WaitForCallbackDocumentAsync(page, callback, state);
                BrowserEvidence.Mark(BrowserPhase.CallbackValidation);
                foreach (var (expectedCallback, expectedState) in new[]
                {
                    ("https://foreign.example.invalid/callback", state),
                    ("https://callback.example.invalid/other", state),
                    (callback, "wrong-state")
                })
                {
                    await Assert.ThrowsAsync<TimeoutException>(() => BrowserJourneys.WaitForCallbackDocumentAsync(page, expectedCallback, expectedState, 250));
                }
                await page.GotoAsync(callback + "?state=" + state + "&state=" + state);
                await Assert.ThrowsAsync<TimeoutException>(() => BrowserJourneys.WaitForCallbackDocumentAsync(page, callback, state, 250));
            }
            finally
            {
                release.TrySetResult();
                await page.UnrouteAllAsync(new()
                {
                    Behavior = UnrouteBehavior.Wait
                });
            }
        });
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Actual_axe_scan_rejects_seeded_contrast_and_accepts_the_corrected_page(string provider)
    {
        Assert.True(provider is "PostgreSql" or "SqlServer");
        await BrowserEvidence.RunAsync(async () =>
        {
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await page.SetContentAsync("<!doctype html><html lang='en'><head><title>Contrast control</title></head><body><main><h1>Contrast control</h1><p id='contrast-probe' style='color:#aaaaaa;background:#ffffff'>Synthetic contrast control</p></main></body></html>");
            // Simulate a later diagnostic changing styles: it must never turn the original failed scan into a pass.
            var contrast = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => BrowserAccessibility.RequireNoViolationsAsync(page,
                () => page.Locator("#contrast-probe").EvaluateAsync("element => element.style.color = '#000000'")));
            Assert.True(contrast.Message.Contains("color-contrast: #contrast-probe", StringComparison.Ordinal) &&
                contrast.Message.Contains("foreground=#aaaaaa, background=#ffffff", StringComparison.Ordinal) &&
                contrast.Message.Contains("after diagnostic capture (later state)", StringComparison.Ordinal) &&
                contrast.Message.Contains("#contrast-probe: color=#000000", StringComparison.Ordinal),
                "The actual axe scan must detect the seeded contrast fault and preserve its safe color evidence.");
            await BrowserAccessibility.RequireNoViolationsAsync(page);
        });
    }
}
