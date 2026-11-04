using System.Globalization;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Uses shipped editor controls and exact safe response headers without printing successful one-time output.</summary>
internal static class ApplicationLifecycleUi
{
    internal const string Applications = "/applications";
    internal const string Collection = "/bff/api/v1/applications";
    internal const string Secret = "#created-client-secret";
    internal const string Heading = "#editor-heading";
    internal const string Create = "Create application";
    internal const string Replace = "Replace secret";
    internal const string Problem = "Review application status";
    internal const string Client = "transport-client";
    internal const int ShortLifetimeSeconds = 60;
    internal const int LongLifetimeSeconds = 1800;
    private const string Display = "Transport client";
    private const string Callback = "https://managed.example.test/callback";
    private const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string ClientField = "#editor-clientId";
    private const string DisplayField = "#editor-displayName";
    private const string RedirectsField = "#editor-redirectUris";
    private const string ExpiryField = "#editor-credentialExpiresAt";
    private const string ConfirmationField = "#editor-confirmed";
    private const string Post = "POST";
    private const string CreatedTitle = "Application created";
    private const string DetailsTitle = "Application details";
    private const string NoStore = "no-store";
    private const string Cache = "cache-control";
    private const string Hide = "I’ve saved it — hide secret";
    private const int Created = 201;
    private const int SecretLength = 44;

    /// <summary>Locates only the named visible user action.</summary>
    internal static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>Requires the actual editor heading with no secret-bearing diagnostic value.</summary>
    internal static Task HeadingAsync(IPage page, string value) => Assertions.Expect(page.Locator(Heading)).ToHaveTextAsync(value);

    /// <summary>Prepares a valid explicit command through the visible form; it does not dispatch a write.</summary>
    internal static async Task PrepareAsync(IPage page, Uri origin, DateTimeOffset now, int lifetime)
    {
        await page.GotoAsync(new Uri(origin, Applications).AbsoluteUri);
        await Button(page, Create).ClickAsync();
        await HeadingAsync(page, Create);
        await page.Locator(ClientField).FillAsync(Client);
        await page.Locator(DisplayField).FillAsync(Display);
        await page.Locator(RedirectsField).FillAsync(Callback);
        await page.Locator(ExpiryField).FillAsync(now.AddSeconds(lifetime).ToString(ExpiryFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>Observes successful native wire output before reading the transient field privately.</summary>
    internal static async Task<string> CreateAsync(IPage page)
    {
        var response = await page.RunAndWaitForResponseAsync(() => Button(page, Create).ClickAsync(), response =>
            new Uri(response.Url).AbsolutePath == Collection && response.Request.Method == Post);
        Assert.Equal(Created, response.Status);
        RequireNoStore(response);
        await HeadingAsync(page, CreatedTitle);
        var secret = await page.Locator(Secret).InputValueAsync();
        Assert.True(secret.Length == SecretLength);
        return secret;
    }

    /// <summary>Starts a replacement draft from the original successful observation without acquiring a new details deadline.</summary>
    internal static async Task ReplacementAsync(IPage page, DateTimeOffset now)
    {
        await Button(page, Hide).ClickAsync();
        await HeadingAsync(page, DetailsTitle);
        await Button(page, Replace).ClickAsync();
        await HeadingAsync(page, Replace);
        await page.Locator(ExpiryField).FillAsync(now.AddSeconds(LongLifetimeSeconds).ToString(ExpiryFormat, CultureInfo.InvariantCulture));
        await page.Locator(ConfirmationField).CheckAsync();
    }

    /// <summary>Retains the exact mutation-response cache contract rather than accepting any permissive cache policy.</summary>
    internal static void RequireNoStore(IResponse response) =>
        Assert.True(response.Headers.TryGetValue(Cache, out var value) && value == NoStore);
}
