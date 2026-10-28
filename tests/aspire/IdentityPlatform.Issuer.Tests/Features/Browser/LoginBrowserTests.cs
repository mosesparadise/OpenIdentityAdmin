using System.Security.Cryptography;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

[Collection("issuer")]
[Trait("Category", "Browser")]
public sealed class LoginBrowserTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Themes = ["light", "dark"];
    private static readonly int[] Widths = [320, 1280];
    public static IEnumerable<TheoryDataRow<string, string, int>> VisualCases =>
        from provider in Providers
        from theme in Themes
        from width in Widths
        select new TheoryDataRow<string, string, int>(provider, theme, width).WithTrait("Provider", provider);

    /// <summary>Requires trusted HTTPS positives and exact native certificate evidence from one isolated untrusted navigation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ExplicitBrowserTrust_When_UnrelatedCertificateIsPresented_Then_CertificateValidationRejectsIt(string provider)
    {
        const string Alive = "/alive";
        const int SecretBytes = 32;
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            BrowserEvidence.Mark(BrowserPhase.TrustedIssuer);
            Assert.Equal(200, (await page.GotoAsync(new Uri(environment.Issuer, Alive).AbsoluteUri))!.Status);
            BrowserEvidence.Mark(BrowserPhase.TrustedRelyingParty);
            Assert.Equal(200, (await page.GotoAsync(new Uri(environment.RelyingParty.BaseUri, Alive).AbsoluteUri))!.Status);
            using var certificate = BrowserCertificate.Create();
            await using var untrusted = new TestRelyingParty { ClientSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretBytes)) };
            await untrusted.StartAsync(TestContext.Current.CancellationToken, certificate);
            BrowserEvidence.Mark(BrowserPhase.UntrustedCertificate);
            BrowserCertificateEvidence.AssertControls();
            await BrowserCertificateEvidence.RequireAsync(session.Context, new Uri(untrusted.BaseUri, Alive).AbsoluteUri);
        });
    }

    /// <summary>Requires genuine keyboard login, restrictive headers, zero CSP violations and no browser-held tokens.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_KeyboardCredentials_When_RealLoginCompletes_Then_StrictCspAndBrowserTokenSecrecyHold(string provider)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            await LoginKeyboardJourney.RequireAsync(environment);
        });
    }

    [Theory]
    [MemberData(nameof(VisualCases))]
    public async Task Both_themes_reflow_and_have_no_automated_accessibility_violations(string provider, string theme, int width)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            // Reproduce the reported initial-context dark desktop finding; each context must pass its first scan.
            var repetitions = BrowserSession.Engine == "webkit" && theme == "dark" && width == 1280 ? 100 : 1;
            for (var iteration = 0; iteration < repetitions; iteration++)
            {
                await using var session = await BrowserSession.CreateAsync(width);
                var context = session.Context;
                var page = await context.NewPageAsync();
                var flow = environment.RelyingParty.Prepare();
                await page.GotoAsync(environment.RelyingParty.StartUri(flow).AbsoluteUri);
                await page.GetByLabel("Theme", new()
                {
                    Exact = true
                }).SelectOptionAsync(theme);
                Assert.Equal(theme, await page.Locator("html").GetAttributeAsync("data-theme"));
                Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
                await BrowserAccessibility.RequireNoViolationsAsync(page);
                await BrowserVisuals.CaptureAsync(page, $"login-{theme}-{width}");
                var keys = await page.EvaluateAsync<string[]>("Object.keys(localStorage)");
                Assert.True(keys.Length == 1 && keys[0] == "identity-theme", "Browser storage must contain only the theme preference.");
                await page.ReloadAsync();
                Assert.Equal(theme, await page.Locator("html").GetAttributeAsync("data-theme"));
                await page.Locator("#username").FillAsync("unknown.visual.user");
                await page.Locator("#password").FillAsync("synthetic-invalid-password");
                await page.Locator("#submit").ClickAsync();
                await RequireGenericErrorAsync(page);
                await page.Locator("#username").FillAsync("");
                await page.Locator("#error-summary").FocusAsync();
                Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
                await BrowserAccessibility.RequireNoViolationsAsync(page, () => BrowserVisuals.CaptureAsync(page, $"error-{theme}-{width}"));
                Assert.Equal(theme, await page.Locator("html").GetAttributeAsync("data-theme"));
                await BrowserVisuals.CaptureAsync(page, $"error-{theme}-{width}");
                // The fresh-context probe repeats the original scan; interactive states need one check per visual case.
                if (iteration == 0)
                {
                    await page.Locator("#submit").HoverAsync();
                    Assert.True(await page.Locator("#submit").EvaluateAsync<bool>("element => element.matches(':hover')"));
                    await BrowserAccessibility.RequireNoViolationsAsync(page, () => BrowserVisuals.CaptureAsync(page, $"error-{theme}-{width}"));
                    await page.Mouse.MoveAsync(0, 0);
                    Assert.False(await page.Locator("#submit").EvaluateAsync<bool>("element => element.matches(':hover')"));
                    await BrowserAccessibility.RequireNoViolationsAsync(page, () => BrowserVisuals.CaptureAsync(page, $"error-{theme}-{width}"));
                }
                if (width == 1280)
                {
                    await page.SetViewportSizeAsync(768, 900);
                    Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
                }
                Assert.True(await page.EvaluateAsync<bool>("matchMedia('(prefers-reduced-motion: reduce)').matches"));
            }
        });
    }

    internal static async Task RequireGenericErrorAsync(IPage page)
    {
        await Assertions.Expect(page.Locator("#error-summary")).ToBeFocusedAsync();
        Assert.True(await page.Locator("#error-summary").InnerTextAsync() == "Unable to sign in. Check your details or start a new sign-in request.", "Expected the fixed generic credential error.");
        Assert.True(await page.Locator("#password").InputValueAsync() == "", "A rejected password must be cleared.");
        Assert.Equal("error-summary", await page.Locator("#password").GetAttributeAsync("aria-describedby"));
        Assert.Equal("error-summary", await page.Locator("#username").GetAttributeAsync("aria-describedby"));
    }
}
