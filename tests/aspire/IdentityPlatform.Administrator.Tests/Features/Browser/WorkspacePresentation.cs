using System.Globalization;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Checks actual React palette, touch targets, keyboard semantics and reflow; captures only anonymous public content.</summary>
internal static class WorkspacePresentation
{
    private const string SystemTheme = "system";
    private const string DarkTheme = "dark";
    private const string LightTheme = "light";
    private const string Main = "main";
    private const string Heading = "h1";
    private const string ArtifactVariable = "IDENTITY_TEST_ARTIFACTS";
    private const string ArtifactRoot = "artifacts";
    private const string TestsDirectory = "tests";
    private const string BrowserDirectory = "browser";
    private const string ImageDirectory = "workspace-presentation";
    private const string ImageExtension = ".png";

    private const string Navigation = "Navigation";
    private const string Enter = "Enter";
    private const string ExpandedValue = "true";
    private const string CollapsedValue = "false";
    private const string Expanded = "aria-expanded";
    private const string Workspace = "Workspace";
    private const string Session = "Your session";
    private const string Appearance = "Appearance";
    private const string Palette = "dark => getComputedStyle(document.body).backgroundColor === (dark ? 'rgb(2, 6, 23)' : 'rgb(248, 250, 252)')";
    private const string Layout = "() => document.documentElement.scrollWidth <= innerWidth && [...document.querySelectorAll('button,select,a.button')].filter(e=>e.getClientRects().length).every(e=>e.getBoundingClientRect().height>=44)";
    private const string Storage = "() => Object.keys(localStorage).every(key=>key==='identity-theme') && sessionStorage.length===0 && document.cookie===''";
    private static readonly int[] Widths = [320, 768, 1280];
    private static readonly bool[] Themes = [false, true];
    private const int Height = 900;

    /// <summary>Exercises explicit and live OS themes plus all required widths without relying on Razor-only inline-theme assertions.</summary>
    internal static async Task RequireAsync(IPage page, string provider)
    {
        var theme = page.GetByRole(AriaRole.Combobox, new()
        {
            Name = Appearance
        });
        await theme.SelectOptionAsync(SystemTheme);
        await page.EmulateMediaAsync(new()
        {
            ColorScheme = ColorScheme.Dark,
            ReducedMotion = ReducedMotion.Reduce
        });
        Assert.True(await page.EvaluateAsync<bool>(Palette, true));
        await page.EmulateMediaAsync(new()
        {
            ColorScheme = ColorScheme.Light
        });
        Assert.True(await page.EvaluateAsync<bool>(Palette, false));
        foreach (var dark in Themes)
        {
            await theme.SelectOptionAsync(dark ? DarkTheme : LightTheme);
            await page.ReloadAsync();
            Assert.True(await theme.InputValueAsync() == (dark ? DarkTheme : LightTheme));
            foreach (var width in Widths)
            {
                await page.SetViewportSizeAsync(width, Height);
                Assert.True(await page.EvaluateAsync<bool>(Palette, dark));
                Assert.True(await page.EvaluateAsync<bool>(Layout));
                await BrowserAccessibility.RequireNoViolationsAsync(page);
                await CaptureAsync(page, provider, dark, width);
                if (width == Widths[0])
                {
                    await MenuAsync(page);
                }
            }
        }
        Assert.True(await page.EvaluateAsync<bool>(Storage));
        await page.SetViewportSizeAsync(Widths[^1], Height);
        Assert.Equal(1, await page.Locator(Main).CountAsync());
        Assert.Equal(1, await page.Locator(Heading).CountAsync());
    }

    /// <summary>Opens and closes the mobile disclosure using a real keyboard while preserving accessible navigation.</summary>
    private static async Task MenuAsync(IPage page)
    {
        var menu = page.GetByRole(AriaRole.Button, new()
        {
            Name = Navigation
        });
        await menu.FocusAsync();
        await page.Keyboard.PressAsync(Enter);
        Assert.Equal(ExpandedValue, await menu.GetAttributeAsync(Expanded));
        Assert.True(await page.GetByRole(AriaRole.Navigation, new()
        {
            Name = Workspace
        }).GetByRole(AriaRole.Link, new()
        {
            Name = Session
        }).IsVisibleAsync());
        await BrowserAccessibility.RequireNoViolationsAsync(page);
        await page.Keyboard.PressAsync(Enter);
        Assert.Equal(CollapsedValue, await menu.GetAttributeAsync(Expanded));
    }

    /// <summary>Observes only storage keys and emptiness, never retaining cookie, token or identity values.</summary>
    internal static async Task RequireStorageAsync(IPage page) => Assert.True(await page.EvaluateAsync<bool>(Storage));

    /// <summary>Checks a live session or failure view without capturing account-bearing pixels or content.</summary>
    internal static async Task RequireStateAsync(IPage page)
    {
        foreach (var width in Widths)
        {
            await page.SetViewportSizeAsync(width, Height);
            Assert.True(await page.EvaluateAsync<bool>(Layout));
            await BrowserAccessibility.RequireNoViolationsAsync(page);
        }
    }

    /// <summary>Names screenshots only with finite provider, retained engine, theme and width; no authenticated identity is captured.</summary>
    private static Task<byte[]> CaptureAsync(IPage page, string provider, bool dark, int width)
    {
        var root = Environment.GetEnvironmentVariable(ArtifactVariable) ?? Path.Combine(ArtifactRoot, TestsDirectory, BrowserDirectory);
        var directory = Path.Combine(root, ImageDirectory);
        Directory.CreateDirectory(directory);
        var name = string.Join('-', provider, BrowserSession.Engine, dark ? DarkTheme : LightTheme, width.ToString(CultureInfo.InvariantCulture)) + ImageExtension;
        return page.ScreenshotAsync(new()
        {
            Path = Path.Combine(directory, name),
            FullPage = true
        });
    }
}
