using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Shares real keyboard/native-form security journeys while keeping random keys and codes out of every persisted artifact.</summary>
internal static class SecurityBrowserJourney
{
    private const string UnfocusedError = "The generic security error did not receive focus.";
    private const string RetainedLogoutAuthority = "Logout retained account authority.";
    private const string UnreachableCodeInput = "The security code input was not reachable in tab order.";
    private const string ReusedRecoveryMaterial = "Regeneration reused previously disclosed recovery material.";
    private const string StoredSecrets = "Account secrets entered browser storage.";
    private const string ViewportOverflow = "A security form overflowed its viewport.";
    private const int Unauthorized = 401;
    private const int ExpectedCodes = 10;
    private const string SignOut = "Sign out";
    private const string Main = "#main";
    private const string ReplaceLink = "Replace your authenticator";
    private const string RegenerateLink = "Create new recovery codes";
    private const string RecoveryLink = "Use a recovery code";
    private const string Theme = "#theme";
    private const string Username = "#username";
    private const string Password = "#password";
    private const string Submit = "#submit";
    private const string Setup = "#setup-key";
    private const string Code = "#code";
    private const string Recovery = "#recovery-code";
    private const string Codes = ".recovery-codes code";
    private const string Enter = "Enter";
    private const string Tab = "Tab";
    private const string Focus = "selector => document.activeElement === document.querySelector(selector)";
    private const string Overflow = "document.documentElement.scrollWidth <= window.innerWidth";
    private const string Storage = "JSON.stringify({local: {...localStorage}, session: {...sessionStorage}})";
    private const string Saved = "I have saved my codes";
    private const string Error = "#error-summary";
    private const string ErrorFocus = "document.activeElement?.id === 'error-summary'";
    private const string WrongPassword = "incorrect-synthetic-password";

    /// <summary>Runs the complete account-security sequence against fresh native state, including a focused generic error and exact logout.</summary>
    internal static async Task RunAsync(AdministratorEnvironment environment, IPage page, bool scripted)
    {
        await PasswordAsync(environment, page, scripted);
        var state = await ConfirmAsync(environment, page, scripted);
        await NavigateAsync(page, ReplaceLink);
        if (scripted)
        {
            await page.Locator(Username).FillAsync(environment.Database.Username);
            await page.Locator(Password).FillAsync(WrongPassword);
            await page.Locator(Submit).PressAsync(Enter);
            await page.Locator(Error).WaitForAsync();
            Assert.True(await page.EvaluateAsync<bool>(ErrorFocus), UnfocusedError);
            await PageAsync(page, scripted);
        }
        await PasswordAsync(environment, page, scripted);
        await CodeAsync(page, FactorOracle.UnusedCode(state.Seed, state.AcceptedCode), recovery: false, scripted);
        state = await ConfirmAsync(environment, page, scripted);
        state = await RegenerateAsync(environment, page, state, scripted);
        await page.GotoAsync(new Uri(environment.Issuer, AdministratorJourney.Login).AbsoluteUri);
        await NavigateAsync(page, RecoveryLink);
        await PasswordAsync(environment, page, scripted);
        await CodeAsync(page, state.RecoveryCodes[0], recovery: true, scripted);
        state = await ConfirmAsync(environment, page, scripted);
        await HistoryAsync(page, state.Seed);
        await page.GotoAsync(new Uri(environment.Issuer, AdministratorJourney.Security).AbsoluteUri);
        await NavigateAsync(page, SignOut);
        await PageAsync(page, scripted);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = SignOut,
            Exact = true
        }).PressAsync(Enter);
        await page.WaitForURLAsync(new Uri(environment.Issuer, AdministratorJourney.Login).AbsoluteUri);
        var response = await page.GotoAsync(new Uri(environment.Issuer, AdministratorJourney.Security).AbsoluteUri);
        Assert.True(response?.Status == Unauthorized, RetainedLogoutAuthority);
    }

    /// <summary>Reaches native password fields through actual tab order when scripts expose the visibility control.</summary>
    private static async Task PasswordAsync(AdministratorEnvironment environment, IPage page, bool scripted)
    {
        await page.Locator(Username).WaitForAsync();
        await PageAsync(page, scripted);
        if (scripted)
        {
            await AdministratorKeyboard.PasswordAsync(page, environment.Database.Username, environment.Database.Password);
        }
        else
        {
            await page.Locator(Username).FillAsync(environment.Database.Username);
            await page.Locator(Password).FillAsync(environment.Database.Password);
            await page.Locator(Submit).PressAsync(Enter);
        }
    }

    /// <summary>Verifies the first body input is keyboard reachable and submits either native TOTP or canonical recovery text.</summary>
    private static async Task CodeAsync(IPage page, string code, bool recovery, bool scripted)
    {
        var selector = recovery ? Recovery : Code;
        await page.Locator(selector).WaitForAsync();
        await PageAsync(page, scripted);
        await page.Locator(scripted ? Theme : Main).FocusAsync();
        await page.Keyboard.PressAsync(Tab);
        Assert.True(await page.EvaluateAsync<bool>(Focus, selector), UnreachableCodeInput);
        await page.Keyboard.InsertTextAsync(code);
        await page.Keyboard.PressAsync(Enter);
    }

    /// <summary>Confirms an actual random staged key, checks its one-time codes and leaves the sensitive response through acknowledgement.</summary>
    private static async Task<SecurityEnrollment> ConfirmAsync(AdministratorEnvironment environment, IPage page, bool scripted)
    {
        await page.Locator(Setup).WaitForAsync();
        await PageAsync(page, scripted);
        var seed = await page.Locator(Setup).InputValueAsync();
        var code = FactorOracle.Code(seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (scripted)
        {
            await AdministratorKeyboard.FactorAsync(page, code);
        }
        else
        {
            await page.Locator(Code).FillAsync(code);
            await page.Locator(Submit).PressAsync(Enter);
        }
        var codes = await SaveAsync(environment, page, seed, scripted);
        return new SecurityEnrollment(seed, code, codes);
    }

    /// <summary>Regenerates only after fresh password and an unused current factor; its new codes replace all previously disclosed codes.</summary>
    private static async Task<SecurityEnrollment> RegenerateAsync(AdministratorEnvironment environment, IPage page, SecurityEnrollment state, bool scripted)
    {
        await NavigateAsync(page, RegenerateLink);
        await PasswordAsync(environment, page, scripted);
        var code = FactorOracle.UnusedCode(state.Seed, state.AcceptedCode);
        await CodeAsync(page, code, recovery: false, scripted);
        var codes = await SaveAsync(environment, page, state.Seed, scripted);
        Assert.False(codes.Intersect(state.RecoveryCodes, StringComparer.Ordinal).Any(), ReusedRecoveryMaterial);
        await HistoryAsync(page, state.Seed);
        return new SecurityEnrollment(state.Seed, code, codes);
    }

    /// <summary>Checks exact code count, storage isolation and accessibility without capturing HTML, screenshots or response diagnostics.</summary>
    private static async Task<string[]> SaveAsync(AdministratorEnvironment environment, IPage page, string seed, bool scripted)
    {
        await page.Locator(Codes).First.WaitForAsync();
        var codes = (await page.Locator(Codes).AllTextContentsAsync()).ToArray();
        Assert.Equal(ExpectedCodes, codes.Length);
        await PageAsync(page, scripted);
        var storage = await page.EvaluateAsync<string>(Storage);
        Assert.False(storage.Contains(seed, StringComparison.Ordinal) || codes.Any(code => storage.Contains(code, StringComparison.Ordinal)),
            StoredSecrets);
        if (scripted)
        {
            await AdministratorKeyboard.AcknowledgeAsync(page);
        }
        else
        {
            await page.GetByRole(AriaRole.Link, new()
            {
                Name = Saved
            }).PressAsync(Enter);
        }
        await page.WaitForURLAsync(new Uri(environment.Issuer, AdministratorJourney.Security).AbsoluteUri);
        return codes;
    }

    /// <summary>Requires native history and reload to keep completed seed/code responses unavailable with or without page scripts.</summary>
    private static async Task HistoryAsync(IPage page, string seed)
    {
        await AdministratorHistory.RequireNoRedisplayAsync(page, seed);
        await AdministratorHistory.ObserveAsync(page, seed, () => page.GoBackAsync());
        await AdministratorHistory.ObserveAsync(page, seed, () => page.ReloadAsync());
        await AdministratorHistory.ObserveAsync(page, seed, () => page.GoForwardAsync());
    }

    /// <summary>Discovers each security action through real forward tab order and native keyboard link activation.</summary>
    private static async Task NavigateAsync(IPage page, string name)
    {
        const int MaximumControls = 12;
        const string Href = "href";
        const string IsFocused = "element => document.activeElement === element";
        const string Unreachable = "The account security action was not reachable through keyboard navigation.";
        var link = page.GetByRole(AriaRole.Link, new()
        {
            Name = name,
            Exact = true
        });
        await page.Locator(Main).FocusAsync();
        for (var index = 0; index < MaximumControls; index++)
        {
            await page.Keyboard.PressAsync(Tab);
            if (await link.EvaluateAsync<bool>(IsFocused))
            {
                var destination = new Uri(new Uri(page.Url), (await link.GetAttributeAsync(Href))!);
                await page.Keyboard.PressAsync(Enter);
                await page.WaitForURLAsync(destination.AbsoluteUri);
                return;
            }
        }
        Assert.Fail(Unreachable);
    }

    /// <summary>Checks actual viewport bounds and runs the existing secret-safe axe wrapper for scripted accessibility acceptance.</summary>
    private static async Task PageAsync(IPage page, bool scripted)
    {
        Assert.True(await page.EvaluateAsync<bool>(Overflow), ViewportOverflow);
        if (scripted)
        {
            await BrowserAccessibility.RequireNoViolationsAsync(page);
        }
    }
}
