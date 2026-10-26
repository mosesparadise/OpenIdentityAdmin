using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Checks native credential pages without screenshots, DOM retention or credential-bearing assertion output.</summary>
internal static class BrowserLoginPresentation
{
    private const string Theme = "#theme";
    private const string Main = "main";
    private const string Heading = "h1";
    private const string Error = "#error-summary";
    private const string Denied = "Unable to complete this request. Check your details or start again.";
    private const string Invalid = "aria-invalid";
    private const string DescribedBy = "aria-describedby";
    private const string True = "true";
    private const string ErrorId = "error-summary";
    private const string CodeDescription = "code-help error-summary";
    private const string Code = "#code";
    private const string Submit = "#submit";
    private const string Tab = "Tab";
    private const string Enter = "Enter";
    private const string Focus = "selector => document.activeElement === document.querySelector(selector)";
    private const string VisibleFocus = "selector => { const element=document.querySelector(selector); const style=getComputedStyle(element); return document.activeElement===element && parseFloat(style.outlineWidth)>0 && style.outlineStyle!=='none' && style.outlineColor!=='transparent'; }";
    private const string Layout = "document.documentElement.scrollWidth <= innerWidth && [...document.querySelectorAll('input:not([type=hidden]),button,select')].filter(e=>!e.hidden && e.getClientRects().length).every(e=>e.getBoundingClientRect().height >= 24)";
    private const string Colors = "dark => { const body=getComputedStyle(document.body); return body.backgroundColor === (dark ? 'rgb(2, 6, 23)' : 'rgb(248, 250, 252)') && body.color === (dark ? 'rgb(248, 250, 252)' : 'rgb(15, 23, 42)'); }";
    private static readonly int[] Widths = [320, 768, 1280];
    private const int Height = 900;
    private const string Light = "light";
    private const string Dark = "dark";

    /// <summary>Checks explicit native theme, semantic structure, actual palette, reflow and axe at each required viewport.</summary>
    internal static async Task PageAsync(IPage page, bool dark)
    {
        await page.Locator(Theme).SelectOptionAsync(dark ? Dark : Light);
        Assert.Equal(1, await page.Locator(Main).CountAsync());
        Assert.Equal(1, await page.Locator(Heading).CountAsync());
        foreach (var width in Widths)
        {
            await page.SetViewportSizeAsync(width, Height);
            Assert.True(await page.EvaluateAsync<bool>(Colors, dark));
            Assert.True(await page.EvaluateAsync<bool>(Layout));
            await BrowserAccessibility.RequireNoViolationsAsync(page);
        }
    }

    /// <summary>Requires the native generic summary to own focus and each rejected field to reference it with preserved MFA help.</summary>
    internal static async Task ErrorAsync(IPage page, params string[] fields)
    {
        await page.Locator(Error).WaitForAsync();
        Assert.True(await page.EvaluateAsync<bool>(Focus, Error));
        await FocusAsync(page, Error);
        Assert.True((await page.Locator(Error).InnerTextAsync()).Trim() == Denied);
        foreach (var field in fields)
        {
            Assert.True(await page.Locator(field).GetAttributeAsync(Invalid) == True);
            Assert.True(await page.Locator(field).GetAttributeAsync(DescribedBy) == (field == Code ? CodeDescription : ErrorId));
            Assert.True(string.IsNullOrEmpty(await page.Locator(field).InputValueAsync()));
        }
    }

    /// <summary>Reaches the real native MFA input and submit through tab order, without forcing focus onto either field.</summary>
    internal static async Task FactorAsync(IPage page, string code)
    {
        await page.Locator(Theme).FocusAsync();
        await page.Keyboard.PressAsync(Tab);
        Assert.True(await page.EvaluateAsync<bool>(Focus, Code));
        await FocusAsync(page, Code);
        await page.Keyboard.InsertTextAsync(code);
        await page.Keyboard.PressAsync(Tab);
        Assert.True(await page.EvaluateAsync<bool>(Focus, Submit));
        await FocusAsync(page, Submit);
        await page.Keyboard.PressAsync(Enter);
    }

    /// <summary>Checks visible keyboard focus from actual computed styling without retaining rendered credential-page content.</summary>
    internal static async Task FocusAsync(IPage page, string selector) => Assert.True(await page.EvaluateAsync<bool>(VisibleFocus, selector));

    /// <summary>Traverses actual recovery input and submit order after the theme control without directly focusing either target.</summary>
    internal static async Task RecoveryAsync(IPage page, string code)
    {
        await page.Locator(Theme).FocusAsync();
        await page.Keyboard.PressAsync(Tab);
        await FocusAsync(page, Recovery);
        await page.Keyboard.InsertTextAsync(code);
        await page.Keyboard.PressAsync(Tab);
        await FocusAsync(page, RecoverySubmit);
        await page.Keyboard.PressAsync(Enter);
    }

    /// <summary>Restores the already-verified summary after visual scans, then reaches the restart link through forward tab order.</summary>
    internal static async Task RestartAsync(IPage page, Uri destination)
    {
        await page.Locator(Error).FocusAsync();
        await page.Keyboard.PressAsync(Tab);
        await FocusAsync(page, Restart);
        var response = await page.RunAndWaitForResponseAsync(() => page.Keyboard.PressAsync(Enter),
            response => response.Request.IsNavigationRequest && response.Request.Frame == page.MainFrame &&
                response.Request.Method == HttpMethods.Get && response.Url == destination.AbsoluteUri);
        Assert.Equal(StatusCodes.Status200OK, response.Status);
        await page.WaitForURLAsync(destination.AbsoluteUri);
        Assert.Equal(1, await page.Locator(RestartForm).CountAsync());
    }
    private const string RestartForm = "form[action='/account/administrator/login'] #username";
    private const string Recovery = "#recovery-code";
    private const string RecoverySubmit = "form[action='/account/administrator/recovery/verify'] button";
    private const string Restart = "a[href='/account/administrator/login']";
}
