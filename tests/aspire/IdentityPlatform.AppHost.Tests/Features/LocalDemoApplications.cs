using Microsoft.Playwright;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Exercises the real application list within each disposable native Aspire demo.</summary>
internal static class LocalDemoApplications
{
    private const string Address = "https://bff.dev.localhost:17444/applications";
    private const string Collection = "/bff/api/v1/applications";
    private const string Registered = "Registered applications";
    private const string Empty = "No matching applications";
    private const string PrefixLabel = "Client ID starts with";
    private const string Search = "Search";
    private const string Missing = "absent-local-demo-%_";
    private const string Rows = "tbody tr";
    private const string RegisteredClient = "identity-admin-bff";
    private const int Success = 200;

    /// <summary>Requires a genuine successful API-backed read and literal empty search before returning to the existing logout flow.</summary>
    internal static async Task RequireAsync(IPage page)
    {
        var response = await page.RunAndWaitForResponseAsync(() => page.GotoAsync(Address),
            value => value.Url.Contains(Collection, StringComparison.Ordinal));
        Assert.Equal(Success, response.Status);
        await page.GetByRole(AriaRole.Heading, new()
        {
            Name = Registered,
            Exact = true
        }).WaitForAsync();
        await page.GetByRole(AriaRole.Cell, new()
        {
            Name = RegisteredClient,
            Exact = true
        }).WaitForAsync();
        await page.GetByLabel(PrefixLabel, new()
        {
            Exact = true
        }).FillAsync(Missing);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = Search,
            Exact = true
        }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new()
        {
            Name = Empty,
            Exact = true
        }).WaitForAsync();
        Assert.Equal(0, await page.Locator(Rows).CountAsync());
    }
}
