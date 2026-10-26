using System.Runtime.ExceptionServices;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Preserves the original secret-safe test failure when independently joined browser cleanup also fails.</summary>
internal static class BrowserTrafficLifetime
{
    /// <summary>Owns one observer until body and cleanup finish; cleanup can never turn a failed body into a passing test.</summary>
    internal static async Task RunAsync(IBrowserContext context, Func<BrowserPrivateTraffic, Task> action)
    {
        var traffic = new BrowserPrivateTraffic(context);
        Exception? primary = null;
        try
        {
            await action(traffic);
        }
        catch (Exception exception) { primary = exception; }
        try
        {
            await traffic.DisposeAsync();
        }
        catch (Exception) when (primary is not null)
        {
            try
            {
                TestContext.Current.TestOutputHelper?.WriteLine(CleanupFailure);
            }
            catch (Exception) { /* Preserve the original failure. */ }
        }
        if (primary is not null)
        {
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
    }
    private const string CleanupFailure = "traffic-cleanup failed; original test failure retained; sensitive diagnostics suppressed.";
}
