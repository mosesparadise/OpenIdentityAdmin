using System.Globalization;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Checks actual themed browser documents using finite observations and screenshots of fixed, nonpersonal visible content only.</summary>
internal static class BrowserLogoutPresentation
{
    private const string ArtifactVariable = "IDENTITY_TEST_ARTIFACTS";
    private const string ArtifactRoot = "artifacts";
    private const string TestDirectory = "tests";
    private const string BrowserDirectory = "browser";
    private const string PresentationDirectory = "b3-presentation";
    private const string ImageExtension = ".png";
    private const string Theme = "#theme";
    private const string Light = "light";
    private const string Dark = "dark";
    private const string System = "system";
    private const string Main = "main";
    private const string Heading = "h1";
    private const string Viewport = "meta[name=viewport]";
    private const string ViewportContent = "width=device-width, initial-scale=1";
    private const string Content = "content";
    private const string Colors = "dark => { const body = getComputedStyle(document.body); return body.backgroundColor === (dark ? 'rgb(2, 6, 23)' : 'rgb(248, 250, 252)') && body.color === (dark ? 'rgb(248, 250, 252)' : 'rgb(15, 23, 42)') && body.getPropertyValue('--accent').trim() === (dark ? '#818cf8' : '#4f46e5'); }";
    private const string Layout = "document.documentElement.scrollWidth <= innerWidth && [...document.querySelectorAll('button, select')].filter(e => !e.hidden).every(e => e.getBoundingClientRect().height >= 44 && e.scrollHeight <= e.clientHeight && e.scrollWidth <= e.clientWidth)";
    private const string Focus = "selector => { const element = document.querySelector(selector); const style = getComputedStyle(element); return document.activeElement === element && parseFloat(style.outlineWidth) >= 3 && style.outlineStyle !== 'none'; }";
    private const string Tab = "Tab";
    private const string BackTab = "Shift+Tab";
    private const string Enter = "Enter";
    private const string Space = "Space";
    private const int Height = 900;
    private static readonly int[] Widths = [320, 768, 1280];

    /// <summary>Checks the initial rendered OS palette, live system changes, explicit override and theme-only persistence after navigation.</summary>
    internal static async Task InitializeAsync(IPage page, bool dark, bool? initialDark = null)
    {
        var system = initialDark ?? dark;
        Assert.True(await page.EvaluateAsync<bool>(Colors, system));
        await page.Locator(Theme).SelectOptionAsync(System);
        await page.EmulateMediaAsync(new()
        {
            ColorScheme = system ? ColorScheme.Light : ColorScheme.Dark
        });
        Assert.True(await page.EvaluateAsync<bool>(Colors, !system));
        await page.EmulateMediaAsync(new()
        {
            ColorScheme = system ? ColorScheme.Dark : ColorScheme.Light
        });
        Assert.True(await page.EvaluateAsync<bool>(Colors, system));
        await page.Locator(Theme).SelectOptionAsync(dark ? Light : Dark);
        Assert.True(await page.EvaluateAsync<bool>(Colors, !dark));
        await page.Locator(Theme).SelectOptionAsync(dark ? Dark : Light);
        await page.ReloadAsync();
        Assert.True(await page.Locator(Theme).InputValueAsync() == (dark ? Dark : Light));
        Assert.True(await page.EvaluateAsync<bool>(Colors, dark));
    }

    /// <summary>Requires semantic structure, real computed palette, reflow, touch targets and zero axe violations at each supported layout.</summary>
    internal static async Task RequireAsync(IPage page, bool dark)
    {
        Assert.Equal(1, await page.Locator(Main).CountAsync());
        Assert.Equal(1, await page.Locator(Heading).CountAsync());
        Assert.True(await page.Locator(Heading).TextContentAsync() == await page.TitleAsync());
        Assert.True(await page.Locator(Viewport).GetAttributeAsync(Content) == ViewportContent);
        Assert.True(await page.Locator(Theme).InputValueAsync() == (dark ? Dark : Light));
        foreach (var width in Widths)
        {
            await page.SetViewportSizeAsync(width, Height);
            Assert.True(await page.EvaluateAsync<bool>(Colors, dark));
            Assert.True(await page.EvaluateAsync<bool>(Layout));
            await BrowserAccessibility.RequireNoViolationsAsync(page);
        }
    }

    /// <summary>Reaches the intended action using real forward/backward tab order from the theme control and submits with Enter or Space.</summary>
    internal static async Task SubmitAsync(IPage page, string selector, bool second = false)
    {
        await page.Locator(Theme).FocusAsync();
        await page.Keyboard.PressAsync(Tab);
        await page.Keyboard.PressAsync(BackTab);
        Assert.True(await page.EvaluateAsync<bool>(Focus, Theme));
        await page.Keyboard.PressAsync(Tab);
        if (second)
        {
            await page.Keyboard.PressAsync(Tab);
        }
        Assert.True(await page.EvaluateAsync<bool>(Focus, selector));
        await page.Keyboard.PressAsync(second ? Space : Enter);
    }

    /// <summary>Retains fixed B3 visible document content including its theme control, excluding browser chrome, account pages and hidden fields.</summary>
    internal static async Task CaptureAsync(IPage page, string provider, bool dark, Screen screen)
    {
        var root = Environment.GetEnvironmentVariable(ArtifactVariable) ?? Path.Combine(ArtifactRoot, TestDirectory, BrowserDirectory);
        var directory = Path.Combine(root, PresentationDirectory);
        Directory.CreateDirectory(directory);
        foreach (var width in new[] { Widths[0], Widths[^1] })
        {
            await page.SetViewportSizeAsync(width, Height);
            var name = string.Join('-', provider, BrowserSession.Engine, dark ? Dark : Light, screen.ToString(), width.ToString(CultureInfo.InvariantCulture)) + ImageExtension;
            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(directory, name),
                FullPage = true
            });
        }
    }

    /// <summary>Limits screenshot names and reviewed capture sites to fixed nonpersonal product documents.</summary>
    internal enum Screen
    {
        Login, Logout, Local, Unknown, Issuer, Incomplete, Returned, Error
    }
}
