using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Distinguishes native refusal to restore a no-store POST from an unrelated navigation failure.</summary>
internal static class AdministratorHistory
{
    private const string Chromium = "chromium";
    private const string Firefox = "firefox";
    private const string ChromiumMiss = "net::ERR_CACHE_MISS";
    private const string FirefoxMiss = "NS_ERROR_DOCUMENT_NOT_CACHED";

    /// <summary>Accepts only the selected engine's exact leading cache-miss code; URL mentions and generic failures do not qualify.</summary>
    internal static bool IsCacheMiss(string engine, string message)
    {
        var marker = engine switch
        {
            Chromium => ChromiumMiss,
            Firefox => FirefoxMiss,
            _ => null
        };
        return marker is not null && (message == marker || message.StartsWith(marker + ' ', StringComparison.Ordinal) ||
            message.StartsWith(marker + '\n', StringComparison.Ordinal));
    }

    /// <summary>Performs one history operation; the caller must still inspect the resulting document for secret redisplay.</summary>
    internal static async Task NavigateAsync(Func<Task<IResponse?>> navigation)
    {
        try
        {
            await navigation();
        }
        catch (PlaywrightException exception) when (IsCacheMiss(BrowserSession.Engine, exception.Message))
        {
            // The engine declined the no-store POST; this is not a retry or a secret-absence assertion.
        }
    }
}
