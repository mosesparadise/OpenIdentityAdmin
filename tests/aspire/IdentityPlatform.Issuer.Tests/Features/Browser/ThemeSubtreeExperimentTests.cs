using System.Text.Json;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

/// <summary>Diagnostic-only synthetic paired theme ownership experiment; never provider or acceptance evidence.</summary>
[Trait("Category", "Browser")]
[Trait("Provider", "PostgreSql")]
public sealed class ThemeSubtreeExperimentTests(ITestOutputHelper output)
{
    private const int Samples = 10;
    private const int Pages = 10;
    private enum Arm
    {
        OriginalNoScan, OwnedNoScan, OriginalFull, OwnedFull
    }

    /// <summary>Retains every independent first finding and fails the diagnostic when any original or candidate observation fails.</summary>
    [Fact]
    public async Task Given_ShippedDarkCompactRecoveryLayout_When_ThemeOwnershipIsCompared_Then_AllFindingsRemainVisible()
    {
        var arms = Enum.GetValues<Arm>();
        var findings = 0;
        for (var sample = 0; sample < Samples; sample++)
        {
            for (var offset = 0; offset < arms.Length; offset++)
            {
                var arm = arms[(sample + offset) % arms.Length];
                var finding = await ObserveAsync(arm, sample + 1);
                findings += finding ? 1 : 0;
            }
        }
        output.WriteLine($"THEME_SUBTREE_COMPLETE samples={Samples} arms={arms.Length} contexts={Samples * arms.Length} findings={findings}");
        Assert.True(findings == 0, "Diagnostic findings remain failures; successful candidate observations are not runtime acceptance.");
    }

    /// <summary>Uses fresh contexts, actual shipped scripts, real synthetic form navigation and the original full axe wrapper without pre-scan style probes.</summary>
    private async Task<bool> ObserveAsync(Arm arm, int sample)
    {
        await using var session = await BrowserSession.CreateAsync(width: 320);
        var page = await session.Context.NewPageAsync();
        var fixture = new ThemeSubtreePage(arm is Arm.OwnedNoScan or Arm.OwnedFull);
        await fixture.AttachAsync(page);
        await page.GotoAsync(ThemeSubtreePage.Origin + "/journey/0");
        await page.Locator("#theme").SelectOptionAsync("dark");
        for (var step = 0; step < Pages; step++)
        {
            Assert.Equal("dark", await page.Locator("html").GetAttributeAsync("data-theme"));
            if (arm is Arm.OriginalFull or Arm.OwnedFull)
            {
                try
                {
                    await BrowserAccessibility.RequireNoViolationsAsync(page);
                }
                catch (Xunit.Sdk.XunitException exception) when (exception.Message.StartsWith("Automated accessibility violations:", StringComparison.Ordinal))
                {
                    output.WriteLine($"THEME_SUBTREE sample={sample} arm={arm} step={step} finding=true boundary=full");
                    output.WriteLine(exception.Message);
                    Assert.Equal(step, fixture.Posts);
                    Assert.Equal(0, fixture.UnexpectedRequests);
                    return true;
                }
            }
            if (step + 1 < Pages)
            {
                await page.Locator("#recovery-code").FillAsync("0123456789ABCDEF0123456789ABCDEF");
                await page.Locator("#continue").PressAsync("Enter");
                await page.WaitForURLAsync(ThemeSubtreePage.Origin + "/journey/" + (step + 1));
            }
        }
        var snapshot = await BrowserAccessibility.ReadStylesAsync(page);
        var finding = !Healthy(snapshot);
        output.WriteLine($"THEME_SUBTREE sample={sample} arm={arm} step={Pages - 1} finding={finding.ToString().ToLowerInvariant()} boundary=final");
        output.WriteLine(BrowserAccessibility.DescribeSnapshot(snapshot));
        Assert.Equal(Pages - 1, fixture.Posts);
        Assert.Equal(0, fixture.UnexpectedRequests);
        return finding;
    }

    /// <summary>Observes inheritance state only, not contrast or pixels: requires healthy root/body plus the missing subtree tokens, scheme and font.</summary>
    private static bool Healthy(JsonElement snapshot)
    {
        if (!ContrastBoundaryTests.HasHealthyCanvas(snapshot))
        {
            return false;
        }

        var canvas = snapshot.GetProperty("canvas");
        return canvas.GetProperty("themePageToken").GetString() == "#020617" &&
            canvas.GetProperty("themeControlPageToken").GetString() == "#020617" &&
            canvas.GetProperty("themeScheme").GetString() == "dark" &&
            canvas.GetProperty("themeControlScheme").GetString() == "dark" &&
            canvas.GetProperty("themeFontApplied").GetBoolean() && canvas.GetProperty("themeControlFontApplied").GetBoolean();
    }
}
