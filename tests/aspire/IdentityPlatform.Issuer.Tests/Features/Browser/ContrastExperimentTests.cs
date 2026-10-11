using System.Globalization;
using System.Text.Json;
using Deque.AxeCore.Commons;
using Microsoft.Playwright;
using Newtonsoft.Json;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

[Collection("issuer")]
[Trait("Category", "Browser")]
public sealed class ContrastExperimentTests(ITestOutputHelper output)
{
    private enum Arm
    {
        Reference, NoInitialScan, PartialScan, PageLifecycle, NoInitialScreenshot
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Synthetic_dark_error_page_preserves_contrast_across_scanner_and_page_lifecycles(string provider)
    {
        Assert.True(provider is "PostgreSql" or "SqlServer");
        var samples = ParseSamples(Environment.GetEnvironmentVariable("IDENTITY_CONTRAST_SAMPLES"));
        var arms = Enum.GetValues<Arm>();
        for (var sample = 0; sample < samples; sample++)
        {
            for (var offset = 0; offset < arms.Length; offset++)
            {
                var arm = arms[(sample + offset) % arms.Length];
                output.WriteLine($"Contrast sample {sample + 1}/{samples}: {arm} started");
                await BrowserEvidence.RunAsync(() => RunSampleAsync(arm, sample + 1));
                output.WriteLine($"Contrast sample {sample + 1}/{samples}: {arm} passed");
            }
        }
        output.WriteLine($"CONTRAST_EXPERIMENT_COMPLETE samples={samples} arms={arms.Length} contexts={samples * arms.Length}");
    }

    internal static int ParseSamples(string? value) => value is null ? 1 :
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count is >= 1 and <= 40 ? count :
        throw new InvalidOperationException("Contrast samples must be an integer from 1 to 40.");

    private static async Task RunSampleAsync(Arm arm, int sample)
    {
        await using var session = await BrowserSession.CreateAsync();
        var page = await session.Context.NewPageAsync();
        var fixture = new ContrastExperimentPage();
        await fixture.AttachAsync(page);
        await page.GotoAsync("https://contrast.example.invalid/account/login");
        await page.GetByLabel("Theme", new()
        {
            Exact = true
        }).SelectOptionAsync("dark");
        Assert.True(await page.Locator("html").GetAttributeAsync("data-theme") == "dark", "The experiment requires the dark theme.");
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        switch (arm)
        {
            case Arm.Reference:
            case Arm.NoInitialScreenshot:
                await BrowserAccessibility.RequireNoViolationsAsync(page);
                break;
            case Arm.PartialScan:
                await RunPartialAsync(page);
                break;
            case Arm.PageLifecycle:
                var browser = page.Context.Browser ?? throw new InvalidOperationException("The lifecycle experiment requires an attached browser.");
                var finishingPage = await browser.NewPageAsync(new()
                {
                    ViewportSize = new()
                    {
                        Width = 1,
                        Height = 1
                    }
                });
                try
                {
                    Assert.NotSame(page.Context, finishingPage.Context);
                }
                finally
                {
                    await finishingPage.CloseAsync();
                }
                break;
            case Arm.NoInitialScan:
                break;
            default:
                throw new InvalidOperationException("Unknown contrast experiment arm.");
        }
        if (arm != Arm.NoInitialScreenshot)
        {
            await CaptureAsync(page);
        }
        var keys = await page.EvaluateAsync<string[]>("Object.keys(localStorage)");
        Assert.True(keys.Length == 1 && keys[0] == "identity-theme", "The synthetic page must persist only its theme preference.");
        await page.ReloadAsync();
        Assert.True(await page.Locator("html").GetAttributeAsync("data-theme") == "dark", "The reload must preserve the dark theme.");
        await page.Locator("#username").FillAsync("synthetic.unknown.user");
        await page.Locator("#password").FillAsync("synthetic-invalid-password");
        await page.Locator("#submit").ClickAsync();
        await LoginBrowserTests.RequireGenericErrorAsync(page);
        await page.Locator("#username").FillAsync("");
        await page.Locator("#error-summary").FocusAsync();
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        // The decisive check is unchanged: no screenshot/style probe/retry precedes this full scan.
        await BrowserAccessibility.RequireNoViolationsAsync(page, () => CaptureAsync(page, $"contrast-{arm}-{sample}"));
        Assert.True(await page.Locator("html").GetAttributeAsync("data-theme") == "dark", "The error page must preserve the dark theme.");
        Assert.Equal(1, fixture.Posts);
        Assert.Equal(0, fixture.UnexpectedRequests);
    }

    private static async Task RunPartialAsync(IPage page)
    {
        // Match DefaultAxeCoreWrapper at 46f4556d, through the pinned public script provider/JS API.
        // This initial intervention intentionally omits finishRun and is not an acceptance scan.
        var script = new BundledAxeScriptProvider().GetScript();
        await page.EvaluateAsync($"() => {script}");
        await page.EvaluateAsync("() => { axe.configure({ branding: 'axe-core-playwright-nuget' }); }");
        Assert.True(await page.EvaluateAsync<bool>("() => window && window.axe && typeof window.axe.runPartial === 'function'"));
        var options = JsonConvert.SerializeObject(new AxeRunOptions { Iframes = true });
        var partialJson = await page.EvaluateAsync<string>("""
            ([contextObj, optionsStr]) => {
                const context = JSON.parse(contextObj);
                const options = JSON.parse(optionsStr);
                return axe.runPartial(context || document, options).then(results => JSON.stringify(results));
            }
            """, new[] { "null", options });
        using var document = JsonDocument.Parse(partialJson);
        var partial = document.RootElement;
        Assert.True(partial.ValueKind == JsonValueKind.Object && partial.TryGetProperty("results", out var results) &&
            results.ValueKind == JsonValueKind.Array && results.GetArrayLength() > 0,
            "The native partial intervention must return populated rule results.");
        Assert.True(await page.EvaluateAsync<string>("() => JSON.stringify(axe.utils.getFrameContexts(null))") == "[]",
            "The synthetic partial intervention must not contain frame contexts.");
    }

    private static async Task CaptureAsync(IPage page, string? name = null)
    {
        Assert.True(await page.Locator("input[name=Password], input[name=Username]").EvaluateAllAsync<bool>("elements => elements.every(element => element.value === '')"),
            "Synthetic captures must not contain form values.");
        string? path = null;
        if (name is not null)
        {
            var root = Environment.GetEnvironmentVariable("IDENTITY_TEST_ARTIFACTS") ?? Path.Combine("artifacts", "tests", "browser");
            path = Path.Combine(root, "screenshots", name + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }
        await page.ScreenshotAsync(new()
        {
            Path = path,
            FullPage = true,
            Animations = ScreenshotAnimations.Disabled,
            Caret = ScreenshotCaret.Hide
        });
    }
}
