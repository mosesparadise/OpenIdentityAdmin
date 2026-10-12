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

    /// <summary>Requires native Firefox refusal with no received main-frame response or successful commit; a timeout never qualifies.</summary>
    internal static bool IsNativeRejection(string engine, Exception error, int responses, int commits) =>
        engine == Firefox && error is PlaywrightException && IsCacheMiss(engine, error.Message) && responses == 0 && commits == 0;

    /// <summary>Observes one real history operation; successful documents must pass the secret oracle, while exact native refusal requires independent navigation evidence.</summary>
    internal static async Task<HistoryOutcome> ObserveAsync(IPage page, string seed, Func<Task<IResponse?>> navigation)
    {
        var responses = 0;
        var commits = 0;
        EventHandler<IResponse> response = (_, value) =>
        {
            if (value.Request.IsNavigationRequest && value.Frame == page.MainFrame)
            {
                Interlocked.Increment(ref responses);
            }
        };
        EventHandler<IFrame> committed = (_, value) =>
        {
            if (value == page.MainFrame)
            {
                Interlocked.Increment(ref commits);
            }
        };
        page.Response += response;
        page.FrameNavigated += committed;
        try
        {
            try
            {
                await navigation();
            }
            catch (PlaywrightException exception) when (IsCacheMiss(BrowserSession.Engine, exception.Message))
            {
                if (IsNativeRejection(BrowserSession.Engine, exception, Volatile.Read(ref responses), Volatile.Read(ref commits)))
                {
                    return HistoryOutcome.NativeRestoreRejected;
                }
                if (BrowserSession.Engine == Firefox)
                {
                    throw;
                }
            }
            await RequireNoRedisplayAsync(page, seed);
            return HistoryOutcome.DocumentObserved;
        }
        finally
        {
            page.Response -= response;
            page.FrameNavigated -= committed;
        }
    }
}

internal enum HistoryOutcome
{
    DocumentObserved, NativeRestoreRejected
}
