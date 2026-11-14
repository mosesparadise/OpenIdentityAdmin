using System.Globalization;
using IdentityPlatform.Administrator.Tests.Features.Browser;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.AppHost.Tests.Features.Samples;

/// <summary>Clones through the shipped React form and binds the one-time result to a later genuine detail request without retaining response bodies.</summary>
internal static class SampleCloneJourney
{
    internal static readonly Uri Administrator = new("https://bff.dev.localhost:17444/");
    internal static readonly Uri Issuer = new("https://issuer.dev.localhost:17443/");
    private const string EditorHeading = "#editor-heading";
    private const string Applications = "/applications";
    private const string Collection = "/bff/api/v1/applications/";
    private const string Suffix = "/clone";
    private const string Clone = "Clone application";
    private const string Cancel = "Cancel";
    private const string Details = "Application details";
    private const string Cloned = "Application cloned";
    private const string DetailsPrefix = "View details for ";
    private const string Client = "browser-sample-clone";
    private const string Display = "Independent sample clone";
    private const string ClientField = "#editor-clientId";
    private const string DisplayField = "#editor-displayName";
    private const string Callbacks = "#editor-redirectUris";
    private const string Expiry = "#editor-credentialExpiresAt";
    private const string Confirm = "#editor-confirmed";
    private const string Secret = "#created-client-secret";
    private const string Hide = "I’ve saved it — hide secret";
    private const string Get = "GET";
    private const string Post = "POST";
    private const string Precondition = "if-match";
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string ReadOnly = "readonly";
    private const string ApiScope = "orders.read";
    private const string EmailScope = "sample.email";
    private const string Enter = "Enter";
    private const int LifetimeMinutes = 30;
    private const int Created = 201;
    private static readonly int[] Widths = [320, 768, 1280];
    private static readonly string[] Themes = ["light", "dark"];
    private const int Ok = 200;

    /// <summary>Requires review, cancel focus, local confirmation and one keyboard submission before native sign-in becomes possible.</summary>
    internal static async Task<ApplicationEditorRegistration> RunAsync(IPage page, ApplicationEditorRegistration source)
    {
        await page.GotoAsync(new Uri(Administrator, Applications).AbsoluteUri);
        var sourcePath = new Uri(Administrator, Collection + Uri.EscapeDataString(source.Id)).AbsoluteUri;
        var read = await page.RunAndWaitForResponseAsync(() => Button(page, DetailsPrefix + source.ClientId).ClickAsync(),
            response => response.Request.Method == Get && response.Url == sourcePath);
        Assert.Equal(Ok, read.Status);
        var sourceVersion = await ApplicationEditorBinding.HeadersAsync(read);
        await HeadingAsync(page, Details);
        await Button(page, Clone).ClickAsync();
        await HeadingAsync(page, Clone);
        await Button(page, Cancel).ClickAsync();
        await HeadingAsync(page, Details);
        await Assertions.Expect(Button(page, Clone)).ToBeFocusedAsync();
        await Button(page, Clone).ClickAsync();
        await HeadingAsync(page, Clone);
        Assert.NotNull(await page.Locator(Callbacks).GetAttributeAsync(ReadOnly));
        Assert.Equal(source.Callback, await page.Locator(Callbacks).InputValueAsync());
        await Assertions.Expect(page.GetByText(ApiScope, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText(EmailScope, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
        await PresentationAsync(page);
        await Button(page, Clone).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await PresentationAsync(page);
        await page.Locator(ClientField).FillAsync(Client);
        await page.Locator(DisplayField).FillAsync(Display);
        await page.Locator(Expiry).FillAsync(DateTimeOffset.UtcNow.AddMinutes(LifetimeMinutes).ToString(Format, CultureInfo.InvariantCulture));
        await page.Locator(Confirm).CheckAsync();
        return await SubmitAsync(page, source, sourcePath, sourceVersion);
    }

    /// <summary>Binds exact source If-Match, single-send and distinct new revision-one identity, then clears visible credentials before safe presentation checks.</summary>
    private static async Task<ApplicationEditorRegistration> SubmitAsync(IPage page, ApplicationEditorRegistration source, string sourcePath, string sourceVersion)
    {
        var url = sourcePath + Suffix;
        using var submissions = new ApplicationSubmission(page, Post, url);
        var response = await page.RunAndWaitForResponseAsync(() => Button(page, Clone).PressAsync(Enter), item => item.Request.Method == Post && item.Url == url);
        Assert.Equal(Created, response.Status);
        Assert.True(await response.Request.HeaderValueAsync(Precondition) == sourceVersion);
        var version = await ApplicationEditorBinding.HeadersAsync(response);
        await HeadingAsync(page, Cloned);
        var secret = await page.Locator(Secret).InputValueAsync();
        ApplicationEditorBinding.Secret(secret);
        Assert.True(secret != source.Secret);
        await Button(page, Hide).ClickAsync();
        await HeadingAsync(page, Details);
        Assert.Equal(0, await page.Locator(Secret).CountAsync());
        await BrowserSecretOracle.StorageAsync(page, [.. source.Secrets, secret]);
        await PresentationAsync(page);
        await page.GotoAsync(new Uri(Administrator, Applications).AbsoluteUri);
        var read = await page.RunAndWaitForResponseAsync(() => Button(page, DetailsPrefix + Client).ClickAsync(), item =>
            item.Request.Method == Get && item.Url.StartsWith(new Uri(Administrator, Collection).AbsoluteUri, StringComparison.Ordinal) &&
            !item.Url[new Uri(Administrator, Collection).AbsoluteUri.Length..].Contains('/'));
        Assert.Equal(Ok, read.Status);
        var id = Uri.UnescapeDataString(new Uri(read.Url).AbsolutePath[Collection.Length..]);
        Assert.NotEqual(source.Id, id);
        Assert.True(version == ApplicationEditorBinding.Version(id, 1));
        Assert.True(await ApplicationEditorBinding.HeadersAsync(read) == version);
        await HeadingAsync(page, Details);
        submissions.RequireOnce();
        return new(id, Client, source.Callback, secret, source.Secret);
    }

    /// <summary>Checks real clone form and safe result reflow and accessibility in both shipped themes and required widths.</summary>
    private static async Task PresentationAsync(IPage page)
    {
        const string Appearance = "Appearance";
        const string Layout = "document.documentElement.scrollWidth <= innerWidth";
        const int Height = 900;
        foreach (var theme in Themes)
        {
            await page.GetByRole(AriaRole.Combobox, new()
            {
                Name = Appearance
            }).SelectOptionAsync(theme);
            foreach (var width in Widths)
            {
                await page.SetViewportSizeAsync(width, Height);
                Assert.True(await page.EvaluateAsync<bool>(Layout));
                await BrowserAccessibility.RequireNoViolationsAsync(page);
            }
        }
    }

    /// <summary>Uses semantic visible actions rather than internal controller calls.</summary>
    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>Requires each user-visible state to finish before continuing.</summary>
    private static async Task HeadingAsync(IPage page, string name)
    {
        await page.GetByRole(AriaRole.Heading, new()
        {
            Name = name,
            Exact = true
        }).WaitForAsync();
        await Assertions.Expect(page.Locator(EditorHeading)).ToBeFocusedAsync();
    }
}
