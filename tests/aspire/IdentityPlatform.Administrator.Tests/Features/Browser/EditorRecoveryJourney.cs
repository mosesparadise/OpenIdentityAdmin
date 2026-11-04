using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises forms, validation and visible success using only real UI-created applications and native mutations.</summary>
internal static class EditorRecoveryJourney
{
    private const string Created = "Application created";
    private const string Updated = "Application updated";
    private const string Replaced = "Secret replaced";
    private const string ClientError = "clientId";
    private const string DisplayError = "displayName";
    private const string ConfirmationError = "confirmed";
    private const int SecretLength = 44;

    /// <summary>Creates one real registration for authority negatives without repeating presentation matrices owned by the recovery theory.</summary>
    internal static async Task<string[]> RegisterAsync(BrowserHttpsFixture environment, IPage page)
    {
        await page.GotoAsync(new Uri(environment.Profile.ClientUri, EditorRecoveryView.Applications).AbsoluteUri);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Create).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Create);
        await EditorRecoveryView.FillCreateAsync(page, environment.Issuer.Account.Journey.Clock.Now);
        await page.Locator(EditorRecoveryView.DisplayField).FillAsync(EditorRecoveryView.ChangedDisplay);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Create).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, Created);
        var secret = await page.Locator(EditorRecoveryView.Secret).InputValueAsync();
        Assert.True(secret.Length == SecretLength);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Hide).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Details);
        await BrowserSecretOracle.StorageAsync(page, [secret]);
        return [secret];
    }

    /// <summary>Returns only private test memory; generated values never enter assertions, URL state or retained artifacts.</summary>
    internal static async Task<string[]> CreateAndChangeAsync(BrowserHttpsFixture environment, IPage page)
    {
        await page.GotoAsync(new Uri(environment.Profile.ClientUri, EditorRecoveryView.Applications).AbsoluteUri);
        var first = await CreateAsync(page, environment.Issuer.Account.Journey.Clock.Now);
        await HideAsync(page, [first]);
        await EditAsync(page);
        var second = await ReplaceAsync(page, environment.Issuer.Account.Journey.Clock.Now);
        Assert.True(first != second);
        await HideAsync(page, [first, second]);
        return [first, second];
    }

    /// <summary>Requires actual create validation, linked focus and one-time output before inspecting its accessible presentation.</summary>
    private static async Task<string> CreateAsync(IPage page, DateTimeOffset now)
    {
        await EditorRecoveryView.Button(page, EditorRecoveryView.Create).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Create);
        await EditorRecoveryView.PresentationAsync(page);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Create).ClickAsync();
        await EditorRecoveryView.ErrorsAsync(page, ClientError);
        await EditorRecoveryView.FillCreateAsync(page, now);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Create).FocusAsync();
        await page.Keyboard.PressAsync(EditorRecoveryView.Enter);
        await EditorRecoveryView.HeadingAsync(page, Created);
        return await SecretAsync(page);
    }

    /// <summary>Edits presentation only, checking linked validation, immutable identity and saved visible configuration.</summary>
    private static async Task EditAsync(IPage page)
    {
        await EditorRecoveryView.Button(page, EditorRecoveryView.Edit).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Edit);
        await EditorRecoveryView.PresentationAsync(page);
        await page.Locator(EditorRecoveryView.DisplayField).FillAsync(string.Empty);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Save).ClickAsync();
        await EditorRecoveryView.ErrorsAsync(page, DisplayError);
        await page.Locator(EditorRecoveryView.DisplayField).FillAsync(EditorRecoveryView.ChangedDisplay);
        await EditorRecoveryView.Button(page, EditorRecoveryView.Save).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, Updated);
        await EditorRecoveryView.PresentationAsync(page);
    }

    /// <summary>Requires the actual unchecked confirmation and future expiry before replacement succeeds.</summary>
    private static async Task<string> ReplaceAsync(IPage page, DateTimeOffset now)
    {
        await EditorRecoveryView.Button(page, EditorRecoveryView.Replace).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Replace);
        await EditorRecoveryView.PresentationAsync(page);
        Assert.False(await page.Locator(EditorRecoveryView.Confirmation).IsCheckedAsync());
        await EditorRecoveryView.Button(page, EditorRecoveryView.Replace).ClickAsync();
        await EditorRecoveryView.ErrorsAsync(page, ConfirmationError);
        await EditorRecoveryView.FillExpiryAsync(page, now);
        await page.Locator(EditorRecoveryView.Confirmation).CheckAsync();
        await EditorRecoveryView.Button(page, EditorRecoveryView.Replace).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, Replaced);
        return await SecretAsync(page);
    }

    /// <summary>Checks presentation while secret output exists, using the existing sanitized axe diagnostics and no screenshots.</summary>
    private static async Task<string> SecretAsync(IPage page)
    {
        var secret = await page.Locator(EditorRecoveryView.Secret).InputValueAsync();
        Assert.True(secret.Length == SecretLength);
        await EditorRecoveryView.PresentationAsync(page);
        return secret;
    }

    /// <summary>Explicit dismissal restores safe focus and keeps prior generated values out of every observed storage surface.</summary>
    private static async Task HideAsync(IPage page, string[] secrets)
    {
        await EditorRecoveryView.Button(page, EditorRecoveryView.Hide).ClickAsync();
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Details);
        await EditorRecoveryView.PresentationAsync(page);
        await BrowserSecretOracle.StorageAsync(page, secrets);
    }
}
