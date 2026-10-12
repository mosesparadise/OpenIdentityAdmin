using IdentityPlatform.Administrator.Tests.Features.Browser;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Seeds diagnostic errors with synthetic private material and requires a fixed safe failure surface.</summary>
public sealed class BrowserEvidenceTests
{
    private const string Canary = "synthetic-private-browser-canary";
    private const string SensitiveMessage = "https://example.invalid/?code=synthetic-private-browser-canary <input value='synthetic-private-browser-canary'>";
    private const string KnownMessage = "Timeout " + SensitiveMessage;
    private const string DataKey = "private-detail";
    private const string Missing = "The browser failure was not safely replaced.";
    private const string KnownSafeMessage = "Administrator browser failure (Timeout); source lines ; sensitive diagnostics suppressed.";
    private const string UnknownSafeMessage = "Administrator browser failure (unclassified); source lines ; sensitive diagnostics suppressed.";

    private static readonly (string Engine, string Message, bool Expected)[] HistoryCases = [
        ("chromium", "net::ERR_CACHE_MISS at https://example.invalid/", true),
        ("firefox", "NS_ERROR_DOCUMENT_NOT_CACHED\nCall log:", true),
        ("webkit", "net::ERR_CACHE_MISS", false),
        ("firefox", "net::ERR_CACHE_MISS", false),
        ("chromium", "navigation failed at https://example.invalid/net::ERR_CACHE_MISS", false),
        ("firefox", "NS_ERROR_DOCUMENT_NOT_CACHED_SUFFIX", false)];

    /// <summary>Known and unknown Playwright errors and nested timeout errors never retain raw messages, URLs, markup or exception state.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_SecretBearingBrowserFailures_When_DiagnosticsAreSanitized_Then_OnlyFixedCategoriesEscape(string provider)
    {
        Assert.False(string.IsNullOrWhiteSpace(provider));
        (Exception Error, string Expected)[] failures = [(new PlaywrightException(KnownMessage), KnownSafeMessage),
            (new PlaywrightException(SensitiveMessage), UnknownSafeMessage),
            (new TimeoutException(KnownMessage, new InvalidOperationException(SensitiveMessage)), KnownSafeMessage)];
        foreach (var failure in failures)
        {
            failure.Error.Data[DataKey] = Canary;
            var result = await Record.ExceptionAsync(() => AdministratorBrowserEvidence.RunAsync(() => Task.FromException(failure.Error)));
            Assert.True(result is Xunit.Sdk.XunitException, Missing);
            Assert.True(result.Message == failure.Expected, Missing);
            Assert.False(result.ToString().Contains(Canary, StringComparison.Ordinal), Missing);
            Assert.True(result.InnerException is null && result.Data.Count == 0, Missing);
        }
        foreach (var history in HistoryCases)
        {
            Assert.Equal(history.Expected, AdministratorHistory.IsCacheMiss(history.Engine, history.Message));
        }
        const string Firefox = "firefox";
        const string Chromium = "chromium";
        const string CacheMiss = "NS_ERROR_DOCUMENT_NOT_CACHED";
        var native = new PlaywrightException(CacheMiss);
        Assert.True(AdministratorHistory.IsNativeRejection(Firefox, native, 0, 0));
        Assert.False(AdministratorHistory.IsNativeRejection(Firefox, native, 1, 0));
        Assert.False(AdministratorHistory.IsNativeRejection(Firefox, native, 0, 1));
        Assert.False(AdministratorHistory.IsNativeRejection(Chromium, native, 0, 0));
        Assert.False(AdministratorHistory.IsNativeRejection(Firefox, new TimeoutException(CacheMiss), 0, 0));
        Assert.False(AdministratorHistory.IsNativeRejection(Firefox, new PlaywrightException(SensitiveMessage), 0, 0));
    }
}
