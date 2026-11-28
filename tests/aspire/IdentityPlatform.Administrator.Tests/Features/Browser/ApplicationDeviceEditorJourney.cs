using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises the shipped device registration form using a genuine administrator browser and protected BFF.</summary>
internal static class ApplicationDeviceEditorJourney
{
    private const string Workspace = "/applications";
    private const string Registration = "/bff/api/v1/applications/device-registrations";
    private const string Create = "Create application";
    private const string Profile = "#editor-profile";
    private const string Device = "device";
    private const string Callback = "#editor-redirectUris";
    private const string StaleCallback = "https://client.example/callback";
    private const string Inapplicable = "#editor-redirectUris,#editor-credentialExpiresAt,#editor-origins";
    private const string ErrorLinks = "a[href='#editor-clientId'],a[href='#editor-displayName']";
    private const string Focused = "element => element === document.activeElement";
    private const string ClientInput = "#editor-clientId";
    private const string DisplayInput = "#editor-displayName";
    private const string Client = "browser-managed-display";
    private const string Display = "Meeting room display";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string ClientField = "clientId";
    private const string DisplayField = "displayName";
    private const string Integration = "Client integration settings";
    private const string Example = "pre.integration-example";
    private const string Enter = "Enter";
    private const int FieldCount = 2;

    /// <summary>Uses the accessible editor, verifies the exact outbound fields and checks integration settings against the chosen client.</summary>
    internal static async Task RunAsync(BootstrapFixture fixture, string provider)
    {
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, managed: true);
        await using var session = await BrowserSession.CreateAsync();
        var page = await session.Context.NewPageAsync();
        // Bind the sign-in ceremony to one instant, then align the details observation with the browser clock.
        var clock = environment.Issuer.Account.Journey.Clock;
        clock.Now = DateTimeOffset.UtcNow;
        await ApplicationAccessFixture.SignInAsync(environment, page);
        clock.Now = DateTimeOffset.UtcNow;
        await page.GotoAsync(new Uri(environment.Profile.ClientUri, Workspace).AbsoluteUri);
        var create = page.GetByRole(AriaRole.Button, new()
        {
            Name = Create,
            Exact = true
        });
        await create.ClickAsync();
        await page.Locator(Callback).FillAsync(StaleCallback);
        await page.Locator(Profile).SelectOptionAsync(Device);
        Assert.Equal(0, await page.Locator(Inapplicable).CountAsync());
        await create.PressAsync(Enter);
        var alert = page.GetByRole(AriaRole.Alert);
        await alert.WaitForAsync();
        Assert.True(await alert.EvaluateAsync<bool>(Focused));
        Assert.Equal(FieldCount, await alert.Locator(ErrorLinks).CountAsync());
        await page.Locator(ClientInput).FillAsync(Client);
        await page.Locator(DisplayInput).FillAsync(Display);
        var url = new Uri(environment.Profile.ClientUri, Registration).AbsoluteUri;
        var response = await page.RunAndWaitForResponseAsync(() => create.PressAsync(Enter), item => item.Request.Method == HttpMethod.Post.Method && item.Url == url);
        Assert.Equal((int)System.Net.HttpStatusCode.Created, response.Status);
        Assert.False(string.IsNullOrEmpty(await response.Request.HeaderValueAsync(Csrf)));
        using var body = JsonDocument.Parse(response.Request.PostData!);
        Assert.Equal(FieldCount, body.RootElement.EnumerateObject().Count());
        Assert.Equal(Client, body.RootElement.GetProperty(ClientField).GetString());
        Assert.Equal(Display, body.RootElement.GetProperty(DisplayField).GetString());
        await page.GetByText(Integration, new()
        {
            Exact = true
        }).PressAsync(Enter);
        var example = await page.Locator(Example).InnerTextAsync();
        Assert.Contains(Client, example, StringComparison.Ordinal);
        Assert.Contains(OpenIddictConstants.GrantTypes.DeviceCode, example, StringComparison.Ordinal);
    }
}
