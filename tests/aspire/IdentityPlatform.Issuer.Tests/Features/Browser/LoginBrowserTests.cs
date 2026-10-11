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
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            await using var session = await BrowserSession.CreateAsync();
            var context = session.Context;
            var page = await context.NewPageAsync();
            BrowserEvidence.Mark(BrowserPhase.TrustedIssuer);
            Assert.Equal(200, (await page.GotoAsync(new Uri(environment.Issuer, "/alive").AbsoluteUri))!.Status);
            BrowserEvidence.Mark(BrowserPhase.TrustedRelyingParty);
            Assert.Equal(200, (await page.GotoAsync(new Uri(environment.RelyingParty.BaseUri, "/alive").AbsoluteUri))!.Status);
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=identityplatform-untrusted-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
            // A separately issued leaf produces a precise unknown-issuer error even when Firefox's
            // pinned transport cannot name newer self-signed-certificate error codes.
            using var authorityKey = RSA.Create(2048);
            var authorityRequest = new CertificateRequest("CN=identityplatform-untrusted-authority", authorityKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            authorityRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            authorityRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            using var authority = authorityRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
            using var issued = request.Create(authority, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(4), RandomNumberGenerator.GetBytes(16));
            using var certificate = issued.CopyWithPrivateKey(key);
            await using var untrusted = new TestRelyingParty { ClientSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) };
            await untrusted.StartAsync(TestContext.Current.CancellationToken, certificate);
            BrowserEvidence.Mark(BrowserPhase.UntrustedCertificate);
            var error = await Assert.ThrowsAsync<PlaywrightException>(() => page.GotoAsync(new Uri(untrusted.BaseUri, "/alive").AbsoluteUri));
            var certificateFailure = BrowserSession.Engine switch
            {
                "chromium" => error.Message.Contains("ERR_CERT_AUTHORITY_INVALID", StringComparison.Ordinal),
                "firefox" => error.Message.Contains("SEC_ERROR_UNKNOWN_ISSUER", StringComparison.Ordinal) ||
                    error.Message.Contains("MOZILLA_PKIX_ERROR_SELF_SIGNED_CERT", StringComparison.Ordinal),
                "webkit" => error.Message.Contains("Unacceptable TLS certificate", StringComparison.Ordinal),
                _ => false
            };
            Assert.True(certificateFailure, "The unrelated certificate must fail specifically at certificate validation (" + BrowserEvidence.Classify(error) + ").");
        });
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Genuine_keyboard_login_completes_under_strict_csp_and_keeps_tokens_out_of_browser_storage(string provider)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            await using var session = await BrowserSession.CreateAsync();
            var context = session.Context; // TLS errors stay fatal.
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
            await page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(page.Locator("#main")).ToBeFocusedAsync();
            await page.Locator(".skip-link").FocusAsync();
            Assert.True(await page.Locator(".wordmark").EvaluateAsync<bool>("element => element.tagName === 'SPAN' && !element.hasAttribute('tabindex') && !element.hasAttribute('role')"),
                "The wordmark must remain noninteractive text until a home route exists.");
            await page.Keyboard.PressAsync("Tab");
            Assert.Equal("theme", await page.Locator(":focus").GetAttributeAsync("id"));
            Assert.Equal("username", await page.Locator("#username").GetAttributeAsync("autocomplete"));
            Assert.Equal("current-password", await page.Locator("#password").GetAttributeAsync("autocomplete"));
            await BrowserJourneys.RequireEmptyStorageAsync(page);
            flow.InteractionId = await page.Locator("input[name=Interaction]").InputValueAsync();
            await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(page.Locator("#username")).ToBeFocusedAsync();
            await page.GetByLabel("Username", new()
            {
                Exact = true
            }).FillAsync(environment.Username);
            await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(page.Locator("#password")).ToBeFocusedAsync();
            await page.GetByLabel("Password", new()
            {
                Exact = true
            }).FillAsync(environment.Password);
            await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(page.Locator("#show-password")).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Space");
            Assert.Equal("text", await page.Locator("#password").GetAttributeAsync("type"));
            await page.GetByRole(AriaRole.Button, new()
            {
                Name = "Hide password",
                Exact = true
            }).PressAsync("Space");
            Assert.Equal("password", await page.Locator("#password").GetAttributeAsync("type"));
            await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(page.Locator("#submit")).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Enter");
            await page.WaitForURLAsync(environment.RelyingParty.Callback + "**");
            Assert.True(await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken), flow.ValidationFailure);
            Assert.True(await page.Locator("body").InnerTextAsync() == "Signed in successfully", "Unexpected relying-party result page.");
            Assert.Empty(violations);
            var cookies = await context.CookiesAsync();
            var sessions = cookies.Where(cookie => cookie.Name == "__Host-identity-pilot").ToArray();
            Assert.True(sessions.Length == 1, "Expected exactly one application session cookie.");
            var cookie = sessions[0];
            Assert.True(cookie.Secure);
            Assert.True(cookie.HttpOnly);
            Assert.Equal(SameSiteAttribute.Lax, cookie.SameSite);
            var storage = await context.StorageStateAsync();
            SafeAssertions.ContainsNoSensitiveMaterial(storage, "access_token", "id_token", environment.Password, environment.ClientSecret);
            Assert.True(await page.EvaluateAsync<bool>("Object.keys(sessionStorage).length === 0"), "Unexpected session storage content.");
            await BrowserJourneys.RequireNoTokensAsync(page, context, environment, flow);
        });
    }

    [Theory]
    [MemberData(nameof(VisualCases))]
    public async Task Both_themes_reflow_and_have_no_automated_accessibility_violations(string provider, string theme, int width)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
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
            var axe = await page.RunAxe();
            Assert.Empty(axe.Violations.Select(value => value.Id));
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
            Assert.Empty((await page.RunAxe()).Violations.Select(value => value.Id));
            await BrowserVisuals.CaptureAsync(page, $"error-{theme}-{width}");
            if (width == 1280)
            {
                await page.SetViewportSizeAsync(768, 900);
                Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
            }
            Assert.True(await page.EvaluateAsync<bool>("matchMedia('(prefers-reduced-motion: reduce)').matches"));
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
