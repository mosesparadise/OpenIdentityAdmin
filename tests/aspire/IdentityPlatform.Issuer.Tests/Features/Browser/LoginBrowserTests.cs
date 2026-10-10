using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Deque.AxeCore.Playwright;
using IdentityPlatform.Issuer.Tests.Fixtures;
using IdentityPlatform.Testing;
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

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Browser_accepts_explicitly_trusted_tls_and_rejects_an_unrelated_certificate(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        Assert.Equal(200, (await page.GotoAsync(new Uri(environment.Issuer, "/alive").AbsoluteUri))!.Status);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=identityplatform-untrusted-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        await using var untrusted = new TestRelyingParty { ClientSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) };
        await untrusted.StartAsync(TestContext.Current.CancellationToken, certificate);
        var error = await Assert.ThrowsAsync<PlaywrightException>(() => page.GotoAsync(untrusted.BaseUri.AbsoluteUri));
        Assert.Contains("ERR_CERT_", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Genuine_keyboard_login_completes_under_strict_csp_and_keeps_tokens_out_of_browser_storage(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(); // TLS errors stay fatal.
        var page = await context.NewPageAsync();
        var violations = new List<string>();
        await page.ExposeFunctionAsync("recordCspViolation", (string directive) => violations.Add(directive));
        await page.AddInitScriptAsync("document.addEventListener('securitypolicyviolation', event => window.recordCspViolation(event.effectiveDirective));");
        var flow = environment.RelyingParty.Prepare(exchange: true);
        var response = await page.GotoAsync(environment.RelyingParty.StartUri(flow).AbsoluteUri);
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.Equal("/account/login", new Uri(page.Url).AbsolutePath);
        var headers = await response.AllHeadersAsync();
        Assert.Contains("form-action 'self'", headers["content-security-policy"], StringComparison.Ordinal);
        Assert.True(CacheControlHeaderValue.Parse(headers["cache-control"]).NoStore);
        Assert.Equal("no-referrer", headers["referrer-policy"]);
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("Skip to main content", await page.Locator(":focus").InnerTextAsync());
        Assert.True(await page.Locator(".wordmark").EvaluateAsync<bool>("element => element.tagName === 'SPAN' && !element.hasAttribute('tabindex') && !element.hasAttribute('role')"),
            "The wordmark must remain noninteractive text until a home route exists.");
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("theme", await page.Locator(":focus").GetAttributeAsync("id"));
        flow.InteractionId = await page.Locator("input[name=Interaction]").InputValueAsync();
        await page.GetByLabel("Username", new()
        {
            Exact = true
        }).FillAsync(environment.Username);
        await page.GetByLabel("Password", new()
        {
            Exact = true
        }).FillAsync(environment.Password);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Show password",
            Exact = true
        }).ClickAsync();
        Assert.Equal("text", await page.Locator("#password").GetAttributeAsync("type"));
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Hide password",
            Exact = true
        }).PressAsync("Space");
        Assert.Equal("password", await page.Locator("#password").GetAttributeAsync("type"));
        await page.Locator("#password").PressAsync("Enter");
        await page.WaitForURLAsync(environment.RelyingParty.Callback + "**");
        Assert.True(await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken), flow.ValidationFailure);
        Assert.True(await page.Locator("body").InnerTextAsync() == "Signed in successfully", "Unexpected relying-party result page.");
        Assert.Empty(violations);
        var cookies = await context.CookiesAsync();
        var sessions = cookies.Where(cookie => cookie.Name == "__Host-identity-pilot").ToArray();
        Assert.True(sessions.Length == 1, "Expected exactly one application session cookie.");
        var session = sessions[0];
        Assert.True(session.Secure);
        Assert.True(session.HttpOnly);
        Assert.Equal(SameSiteAttribute.Lax, session.SameSite);
        var storage = await context.StorageStateAsync();
        SafeAssertions.ContainsNoSensitiveMaterial(storage, "access_token", "id_token", environment.Password, environment.ClientSecret);
        Assert.True(await page.EvaluateAsync<bool>("Object.keys(sessionStorage).length === 0"), "Unexpected session storage content.");
    }

    [Theory]
    [MemberData(nameof(VisualCases))]
    public async Task Both_themes_reflow_and_have_no_automated_accessibility_violations(string provider, string theme, int width)
    {
        var environment = await fixture.GetAsync(provider);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new()
            {
                Width = width,
                Height = 900
            },
            ReducedMotion = ReducedMotion.Reduce
        });
        var page = await context.NewPageAsync();
        var flow = environment.RelyingParty.Prepare();
        await page.GotoAsync(environment.RelyingParty.StartUri(flow).AbsoluteUri);
        await page.GetByLabel("Theme", new()
        {
            Exact = true
        }).SelectOptionAsync(theme);
        Assert.Equal(theme, await page.Locator("html").GetAttributeAsync("data-theme"));
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        var axe = await page.RunAxe();
        Assert.Empty(axe.Violations.Select(value => value.Id));
        var root = Environment.GetEnvironmentVariable("IDENTITY_TEST_ARTIFACTS") ?? Path.Combine("artifacts", "tests", provider);
        var path = Path.Combine(root, "screenshots", $"login-{theme}-{width}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var screenshot = await page.ScreenshotAsync(new()
        {
            Path = path,
            FullPage = true,
            Animations = ScreenshotAnimations.Disabled,
            Caret = ScreenshotCaret.Hide
        });
        var keys = await page.EvaluateAsync<string[]>("Object.keys(localStorage)");
        Assert.True(keys.Length == 1 && keys[0] == "identity-theme", "Browser storage must contain only the theme preference.");
        await page.ReloadAsync();
        Assert.Equal(theme, await page.Locator("html").GetAttributeAsync("data-theme"));
        var baseline = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Features", "Browser", "Baselines", $"login-{theme}-{width}.png"), TestContext.Current.CancellationToken);
        var difference = await page.EvaluateAsync<int[]>(ComparePixels, new[] { Convert.ToBase64String(baseline), Convert.ToBase64String(screenshot) });
        Assert.Equal(3, difference.Length);
        Assert.Equal(1, difference[0]);
        Assert.InRange(difference[1], 0, 1);
        Assert.InRange(difference[2], 0, 100);
    }

    // Decode pixels with the already-pinned browser. No image library or relaxed browser policy is required.
    // Native select-border rasterization differed by one channel unit at30 pixels across CI runners.
    private const string ComparePixels = """
        async images => {
            const decode = async base64 => {
                const bytes = Uint8Array.from(atob(base64), value => value.charCodeAt(0));
                const bitmap = await createImageBitmap(new Blob([bytes], {type: 'image/png'}));
                const canvas = new OffscreenCanvas(bitmap.width, bitmap.height);
                const context = canvas.getContext('2d', {willReadFrequently: true});
                context.drawImage(bitmap, 0, 0);
                const pixels = context.getImageData(0, 0, bitmap.width, bitmap.height).data;
                const result = {width: bitmap.width, height: bitmap.height, pixels};
                bitmap.close();
                return result;
            };
            const [expected, actual] = await Promise.all(images.map(decode));
            if (expected.width !== actual.width || expected.height !== actual.height)
                return [0, 255, 2147483647];
            let maxDelta = 0, changedPixels = 0;
            for (let index = 0; index < expected.pixels.length; index += 4) {
                let changed = false;
                for (let channel = 0; channel < 4; channel++) {
                    const delta = Math.abs(expected.pixels[index + channel] - actual.pixels[index + channel]);
                    maxDelta = Math.max(maxDelta, delta);
                    changed ||= delta !== 0;
                }
                if (changed) changedPixels++;
            }
            return [1, maxDelta, changedPixels];
        }
        """;

}
