using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

internal static class BrowserEvidence
{
    public static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            throw new Xunit.Sdk.XunitException("Browser operation failed; sensitive action and URL diagnostics suppressed.");
        }
    }
}
