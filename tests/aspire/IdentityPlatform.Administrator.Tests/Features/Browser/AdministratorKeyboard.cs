using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Traverses real tab order with secret-safe focus checks rather than forcing focus onto the field under test.</summary>
internal static class AdministratorKeyboard
{
    private const string Theme = "#theme";
    private const string Username = "#username";
    private const string Password = "#password";
    private const string Toggle = "#show-password";
    private const string Submit = "#submit";
    private const string SetupKey = "#setup-key";
    private const string Code = "#code";
    private const string Tab = "Tab";
    private const string BackTab = "Shift+Tab";
    private const string Enter = "Enter";
    private const string Space = "Space";
    private const string SelectAll = "ControlOrMeta+A";
    private const string TypeAttribute = "type";
    private const string PlainType = "text";
    private const string PasswordType = "password";
    private const string Focus = "selector => document.activeElement === document.querySelector(selector)";
    private const string Selection = "document.activeElement.selectionStart === 0 && document.activeElement.selectionEnd === document.activeElement.value.length";
    private const string MissingFocus = "A required account control was unreachable in the expected tab order.";
    private const string MissingSelection = "The readonly setup key could not be selected using the keyboard.";

    /// <summary>Starts at the theme control and reaches credentials, visibility toggle and submit using actual forward/backward tabs.</summary>
    internal static async Task PasswordAsync(IPage page, string username, string password)
    {
        await page.Locator(Theme).FocusAsync();
        await MoveAsync(page, Username);
        await page.Keyboard.InsertTextAsync(username);
        await MoveAsync(page, Password);
        await page.Keyboard.InsertTextAsync(password);
        await MoveAsync(page, Toggle);
        await page.Keyboard.PressAsync(Space);
        Assert.True(await page.Locator(Password).GetAttributeAsync(TypeAttribute) == PlainType, MissingFocus);
        await page.Keyboard.PressAsync(Space);
        Assert.True(await page.Locator(Password).GetAttributeAsync(TypeAttribute) == PasswordType, MissingFocus);
        await MoveAsync(page, Submit);
        await MoveAsync(page, Toggle, backwards: true);
        await MoveAsync(page, Submit);
        await page.Keyboard.PressAsync(Enter);
    }

    /// <summary>Reaches the readonly setup key and code input through tab order and proves keyboard selection before confirmation.</summary>
    internal static async Task FactorAsync(IPage page, string code)
    {
        await page.Locator(Theme).FocusAsync();
        await MoveAsync(page, SetupKey);
        await page.Keyboard.PressAsync(SelectAll);
        Assert.True(await page.EvaluateAsync<bool>(Selection), MissingSelection);
        await MoveAsync(page, Code);
        await page.Keyboard.InsertTextAsync(code);
        await MoveAsync(page, Submit);
        await page.Keyboard.PressAsync(Enter);
    }

    /// <summary>Reaches the recovery acknowledgement as the first body control after the theme selector.</summary>
    internal static async Task AcknowledgeAsync(IPage page)
    {
        const string Acknowledgement = "a.primary";
        await page.Locator(Theme).FocusAsync();
        await MoveAsync(page, Acknowledgement);
        await page.Keyboard.PressAsync(Enter);
    }

    /// <summary>Requires the native focus result while avoiding assertion dumps of secret-bearing input elements.</summary>
    private static async Task MoveAsync(IPage page, string selector, bool backwards = false)
    {
        await page.Keyboard.PressAsync(backwards ? BackTab : Tab);
        Assert.True(await page.EvaluateAsync<bool>(Focus, selector), MissingFocus);
    }
}
