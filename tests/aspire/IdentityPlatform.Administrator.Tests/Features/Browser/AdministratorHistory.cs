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
    private const string Root = "html";
    private const int ReadinessMilliseconds = 5000;
    private const string Snapshot = """
        (element, seed) => {
            if (document.readyState !== 'complete') return false;
            if (document.querySelectorAll('.recovery-codes code').length !== 0 || element.outerHTML.includes(seed))
                throw new Error('Committed account secrets reappeared during browser history traversal.');
            return true;
        }
        """;

    /// <summary>Uses driver polling for readiness even without page scripts, then fails immediately on one atomic secret-bearing observation.</summary>
    internal static async Task RequireNoRedisplayAsync(IPage page, string seed, float timeout = ReadinessMilliseconds)
    {
        await page.Locator(Root).WaitForFunctionAsync(Snapshot, seed, new()
        {
            Timeout = timeout
        });
    }

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
