using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves real password/TOTP/recovery paste and readable secret-saving instructions in each required provider/engine cell.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "Accounts")]
public sealed class ClipboardBrowserTests(BootstrapFixture fixture)
{
    private const string Username = "#username";
    private const string Password = "#password";
    private const string Code = "#code";
    private const string Recovery = "#recovery-code";
    private const string Setup = "#setup-key";
    private const string Submit = "#submit";
    private const string Enter = "Enter";
    private const string Autocomplete = "autocomplete";
    private const string UsernameHint = "username";
    private const string PasswordHint = "current-password";
    private const string Codes = ".recovery-codes code";
    private const string Instructions = "Add a time-based account in your authenticator using this setup key. Keep the key private.";
    private const string OnceOnly = "These codes are shown only once. Reloading or leaving this page will not show them again.";
    private const string Acknowledgement = "I have saved my codes";
    private const string ExternalRequest = "An administrator page requested content outside its canonical HTTPS origin.";

    /// <summary>Native clipboard events and accepted real forms establish paste support; autocomplete hints do not claim human password-manager acceptance.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AdministratorForms_When_NativeClipboardPasteSubmitsCredentials_Then_TrustedTransfersCompleteWithoutExternalRequests(string provider)
    {
        await AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
            await using var browser = await BrowserSession.CreateAsync();
            var page = await browser.Context.NewPageAsync();
            var external = 0;
            browser.Context.Request += (_, request) =>
            {
                if (new Uri(request.Url).GetLeftPart(UriPartial.Authority) != environment.Issuer.GetLeftPart(UriPartial.Authority))
                {
                    Interlocked.Exchange(ref external, 1);
                }
            };
            await page.GotoAsync(new Uri(environment.Issuer, AdministratorJourney.Enrollment).AbsoluteUri);
            await PasswordAsync(environment, page);
            await page.Locator(Setup).WaitForAsync();
            Assert.True(await page.GetByText(Instructions, new()
            {
                Exact = true
            }).IsVisibleAsync());
            Assert.Equal("one-time-code", await page.Locator(Code).GetAttributeAsync(Autocomplete));
            var seed = await page.Locator(Setup).InputValueAsync();
            await AdministratorClipboard.PasteAsync(page, Code, FactorOracle.Code(seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            await page.Locator(Submit).PressAsync(Enter);
            await page.Locator(Codes).First.WaitForAsync();
            var codes = await page.Locator(Codes).AllTextContentsAsync();
            Assert.Equal(10, codes.Count);
            Assert.True(await page.GetByText(OnceOnly, new()
            {
                Exact = true
            }).IsVisibleAsync());
            await page.GetByRole(AriaRole.Link, new()
            {
                Name = Acknowledgement,
                Exact = true
            }).PressAsync(Enter);
            await page.WaitForURLAsync(new Uri(environment.Issuer, AdministratorJourney.Security).AbsoluteUri);
            await page.GotoAsync(new Uri(environment.Issuer, SecurityHttp.Recovery).AbsoluteUri);
            await PasswordAsync(environment, page);
            await page.Locator(Recovery).WaitForAsync();
            await AdministratorClipboard.PasteAsync(page, Recovery, codes[0]);
            await page.Locator(Recovery).PressAsync(Enter);
            await page.Locator(Setup).WaitForAsync();
            Assert.True(Volatile.Read(ref external) == 0, ExternalRequest);
        });
    }

    /// <summary>Checks platform-compatible autocomplete tokens and submits a genuinely pasted password through the native form.</summary>
    private static async Task PasswordAsync(AdministratorEnvironment environment, IPage page)
    {
        await page.Locator(Username).WaitForAsync();
        Assert.Equal(UsernameHint, await page.Locator(Username).GetAttributeAsync(Autocomplete));
        Assert.Equal(PasswordHint, await page.Locator(Password).GetAttributeAsync(Autocomplete));
        await page.Locator(Username).FillAsync(environment.Database.Username);
        await AdministratorClipboard.PasteAsync(page, Password, environment.Database.Password);
        await page.Locator(Submit).PressAsync(Enter);
    }
}
