using System.Text.Json;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

[Collection("issuer")]
[Trait("Category", "Browser")]
public sealed class ContrastBoundaryTests(ITestOutputHelper output)
{
    private enum Arm
    {
        NoScan, TargetPartial, FullWrapper
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Synthetic_error_page_observations_isolate_navigation_target_scan_and_finishing_page(string provider)
    {
        Assert.True(provider is "PostgreSql" or "SqlServer");
        var samples = ContrastExperimentTests.ParseSamples(Environment.GetEnvironmentVariable("IDENTITY_CONTRAST_SAMPLES"));
        var arms = Enum.GetValues<Arm>();
        var findings = 0;
        for (var sample = 0; sample < samples; sample++)
        {
            for (var offset = 0; offset < arms.Length; offset++)
            {
                var arm = arms[(sample + offset) % arms.Length];
                output.WriteLine($"Boundary sample {sample + 1}/{samples}: {arm} started");
                var finding = false;
                await BrowserEvidence.RunAsync(async () => finding = await ObserveAsync(arm, sample + 1));
                findings += finding ? 1 : 0;
                output.WriteLine($"Boundary sample {sample + 1}/{samples}: {arm} observed finding={finding.ToString().ToLowerInvariant()}");
            }
        }
        output.WriteLine($"CONTRAST_BOUNDARIES_COMPLETE samples={samples} arms={arms.Length} contexts={samples * arms.Length} findings={findings}");
        Assert.True(findings == 0, $"The boundary experiment recorded {findings} failing observations; all findings remain unresolved failures.");
    }

    private async Task<bool> ObserveAsync(Arm arm, int sample)
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
        Assert.True(await page.Locator("html").GetAttributeAsync("data-theme") == "dark", "The boundary experiment requires the dark theme.");
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        await ContrastExperimentTests.CaptureAsync(page);
        await ContrastExperimentTests.NavigateToErrorAsync(page);

        var finding = false;
        if (arm == Arm.TargetPartial)
        {
            var partial = await ContrastExperimentTests.RunPartialAsync(page);
            var evidence = BrowserAccessibility.DescribePartial(partial);
            output.WriteLine($"Raw target partial: failed={evidence.Failures}, incomplete={evidence.Incomplete}; {evidence.Evidence}");
            finding = evidence.Failures != 0 || evidence.Incomplete != 0;
        }
        else if (arm == Arm.FullWrapper)
        {
            try
            {
                await BrowserAccessibility.RequireNoViolationsAsync(page,
                    () => ContrastExperimentTests.CaptureAsync(page, $"boundary-{arm}-{sample}"));
            }
            catch (Xunit.Sdk.XunitException exception) when (exception.Message.StartsWith("Automated accessibility violations:", StringComparison.Ordinal))
            {
                // Collect independent boundary observations, never convert a failed full scan into a pass.
                output.WriteLine(exception.Message);
                finding = true;
            }
        }
        var snapshot = await BrowserAccessibility.ReadStylesAsync(page);
        output.WriteLine((arm == Arm.FullWrapper && finding ? "Boundary observation after failed scan/diagnostic capture (later state)" : "Observation at assigned boundary") +
            BrowserAccessibility.DescribeSnapshot(snapshot));
        finding |= !HasHealthyCanvas(snapshot);
        Assert.Equal(1, fixture.Posts);
        Assert.Equal(0, fixture.UnexpectedRequests);
        return finding;
    }

    internal static bool HasHealthyCanvas(JsonElement snapshot)
    {
        if (snapshot.ValueKind != JsonValueKind.Object || !snapshot.TryGetProperty("canvas", out var canvas) || canvas.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        bool Matches(string key, string expected) => canvas.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() == expected;
        return Matches("bodyBackgroundKind", "opaque") && Matches("bodyColor", "#f8fafc") && Matches("bodyBackground", "#020617") &&
            Matches("pageToken", "#020617") && Matches("textToken", "#f8fafc") && Matches("rootPageToken", "#020617") && Matches("rootTextToken", "#f8fafc") &&
            Matches("bodyScheme", "dark") && Matches("rootScheme", "dark") && canvas.TryGetProperty("fontApplied", out var font) && font.ValueKind == JsonValueKind.True;
    }
}
