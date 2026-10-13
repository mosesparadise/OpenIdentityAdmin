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
        var partialEvidence = BrowserAccessibility.DescribePartial(System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            results = new[] { new { id = "color-contrast", nodes = new[]
            {
                new { node = new { selector = new[] { ".wordmark", sentinel }, html = sentinel }, any = new[]
                {
                    new { id = "color-contrast", result = (bool?)false, data = new { fgColor = "#f8fafc", bgColor = "#ffffff", contrastRatio = 1.04, secret = sentinel } },
                    new { id = "color-contrast", result = (bool?)null, data = new { fgColor = sentinel, bgColor = sentinel, contrastRatio = 0.0, secret = sentinel } }
                } }
            } } }
        }));
        Assert.Equal(1, partialEvidence.Failures);
        Assert.Equal(1, partialEvidence.Incomplete);
        Assert.True(partialEvidence.Evidence.Contains("foreground=#f8fafc, background=#ffffff, contrast=1.04", StringComparison.Ordinal) &&
            !partialEvidence.Evidence.Contains(sentinel, StringComparison.Ordinal), "Raw partial diagnostics must preserve failed checks without arbitrary values.");
        foreach (var malformed in new object[] { sentinel, new { }, new { canvas = sentinel }, new { canvas = new { bodyBackgroundKind = "opaque" } } })
        {
            Assert.False(ContrastBoundaryTests.HasHealthyCanvas(System.Text.Json.JsonSerializer.SerializeToElement(malformed)),
                "Missing/malformed canvas observations must remain findings.");
        }
        var assertion = new Xunit.Sdk.XunitException("Expected invariant failure");
        var preserved = await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(() => BrowserEvidence.RunAsync(() => Task.FromException(assertion)));
        Assert.Same(assertion, preserved);
        Assert.Throws<InvalidOperationException>(() => BrowserSession.SelectEngine("unsupported"));
        Assert.Equal("chromium", BrowserSession.SelectEngine(null));
        Assert.Equal("firefox", BrowserSession.SelectEngine("firefox"));
        Assert.Equal("webkit", BrowserSession.SelectEngine("webkit"));
        Assert.Equal(1, ContrastExperimentTests.ParseSamples(null));
        Assert.Equal(1, ContrastExperimentTests.ParseSamples("1"));
        Assert.Equal(40, ContrastExperimentTests.ParseSamples("40"));
        foreach (var invalid in new[] { "", "0", "41", "-1", "1.0", " 1", "+1", sentinel })
        {
            var rejected = Assert.Throws<InvalidOperationException>(() => ContrastExperimentTests.ParseSamples(invalid));
            Assert.Equal("Contrast samples must be an integer from 1 to 40.", rejected.Message);
        }
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

    /// <summary>Requires bounded secret-safe channel diagnostics without turning noncertificate failures into certificate acceptance.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public void Given_NativeFailureChannels_When_DiagnosticsAreCaptured_Then_OnlyStrictCertificateEvidenceCanAccept(string provider)
    {
        const string postgreSql = "PostgreSql";
        const string sqlServer = "SqlServer";
        Assert.True(provider is postgreSql or sqlServer);
        BrowserCertificateEvidence.AssertControls();
        const string secretPrefix = "private-canary-";
        const string cancelled = "cancelled";
        const string webkitCertificate = "Unacceptable TLS certificate";
        var secret = secretPrefix + Guid.NewGuid().ToString();
        var observation = new CertificateObservation();
        observation.Observe(false, true, cancelled + secret);
        observation.ObserveNavigation(new PlaywrightException(secret));
        observation.Observe(false, true, null);
        observation.Observe(false, true, "");
        observation.Observe(false, true, secret);
        observation.Observe(true, false, webkitCertificate + secret);
        var failure = observation.Missing(new PlaywrightException(cancelled + secret), true, false);
        Assert.False(observation.HasCertificate);
        Assert.Null(failure.InnerException);
        Assert.DoesNotContain(secret, failure.ToString(), StringComparison.Ordinal);
        Assert.Contains("exact failures=4; unrelated failures=1; page-closed=True; browser-connected=False; dropped=0", failure.Message, StringComparison.Ordinal);
        Assert.Contains("ordinal=1", failure.Message, StringComparison.Ordinal);
        Assert.Contains("channel=request exact=True category=cancelled", failure.Message, StringComparison.Ordinal);
        Assert.Contains("ordinal=2", failure.Message, StringComparison.Ordinal);
        Assert.Contains("channel=navigation exact=True category=unclassified browser error", failure.Message, StringComparison.Ordinal);
        Assert.Contains("category=missing", failure.Message, StringComparison.Ordinal);
        Assert.Contains("category=empty", failure.Message, StringComparison.Ordinal);
        RequireBoundedReceipts(observation, secret);
        Assert.False(observation.HasCertificate);
        observation.Observe(true, true, webkitCertificate);
        Assert.True(observation.HasCertificate);
    }

    /// <summary>Checks the actual retained event list, so a removed storage bound fails even if the dropped counter remains correct.</summary>
    private static void RequireBoundedReceipts(CertificateObservation observation, string secret)
    {
        const int overflowEvents = 10;
        const string ordinalMarker = "ordinal=";
        const string expectedDropped = "dropped=8";
        const string expectedUnknown = "browser-connected=unknown";
        const string expectedOrdinals = "1,2,3,4,5,6,7,8";
        for (var index = 0; index < overflowEvents; index++)
        {
            observation.Observe(false, true, secret);
        }
        var failure = observation.Missing(new PlaywrightException(secret));
        Assert.Contains(expectedDropped, failure.Message, StringComparison.Ordinal);
        Assert.Contains(expectedUnknown, failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, failure.ToString(), StringComparison.Ordinal);
        var retainedOrdinals = failure.Message.Split(ordinalMarker).Skip(1).Select(entry => entry.Split(' ')[0]);
        Assert.Equal(expectedOrdinals, string.Join(',', retainedOrdinals));
    }

}
