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
        RequireProgressFailureIsNonDisruptive();
        RequireResponseFailureIsPrivate();
        (Exception Error, string Expected)[] failures = [(new PlaywrightException(KnownMessage), KnownSafeMessage),
            (new PlaywrightException(SensitiveMessage), UnknownSafeMessage),
            (new TimeoutException(KnownMessage, new InvalidOperationException(SensitiveMessage)), KnownSafeMessage),
            (new AggregateException(new Xunit.Sdk.XunitException(SensitiveMessage), new TimeoutException(SensitiveMessage)), KnownSafeMessage),
            (new AggregateException(new InvalidOperationException(SensitiveMessage), new PlaywrightException(SensitiveMessage)), UnknownSafeMessage)];
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
    /// <summary>A real file/directory collision cannot interrupt a release marker or replace the primary failure during resource disposal.</summary>
    private static void RequireProgressFailureIsNonDisruptive()
    {
        var directory = Path.Combine(Path.GetTempPath(), ProgressPrefix + Guid.NewGuid().ToString(ProgressIdentifierFormat));
        var destination = Path.Combine(directory, ProgressDestination);
        Directory.CreateDirectory(destination);
        try
        {
            var progress = new ApplicationLifecycleProgress(ApplicationLifecycleScenario.Pending, checkpointPath: destination);
            Assert.Null(Record.Exception(() => progress.Mark(ApplicationLifecycleStage.Release)));
            var primary = new InvalidOperationException(SensitiveMessage);
            Action action = () =>
            {
                var cleanup = new ApplicationLifecycleProgress(ApplicationLifecycleScenario.Pending, checkpointPath: destination);
                using var marker = cleanup.BeforeDispose(ApplicationLifecycleDisposal.Environment);
                throw primary;
            };
            var observed = Record.Exception(action);
            Assert.True(ReferenceEquals(primary, observed));
            Assert.True(Directory.Exists(destination));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Known, unknown and navigation-hint body failures produce exact closed diagnostics and cannot publish canaries or disrupt the primary error.</summary>
    private static void RequireResponseFailureIsPrivate()
    {
        const int PrefixLimit = 4096;
        const char Padding = 'x';
        const string Unknown = "Unknown";
        const string Unavailable = "Unavailable";
        const string MissingResource = "MissingResource";
        const string MissingData = "MissingData";
        const string Evicted = "Evicted";
        const string UnavailableMessage = "Response body is unavailable";
        const string MissingResourceMessage = "No resource with given identifier found";
        const string MissingDataMessage = "No data found for resource with given identifier";
        const string EvictedMessage = "Request content was evicted from inspector cache";
        const string NavigationHint = "Response body is not available for a response that was navigated away from.";
        (string Message, string Category)[] cases = [(SensitiveMessage, Unknown),
            (UnavailableMessage, Unavailable), (MissingResourceMessage, MissingResource),
            (MissingDataMessage, MissingData), (EvictedMessage, Evicted), (NavigationHint, Unknown),
            (new string(Padding, PrefixLimit) + UnavailableMessage, Unknown)];
        foreach (var item in cases)
        {
            var error = new PlaywrightException(item.Message + SensitiveMessage);
            error.Data[DataKey] = Canary;
            RequireResponseDiagnostic(error, item.Category);
        }
        var primary = new PlaywrightException(SensitiveMessage);
        Action action = () =>
        {
            ApplicationEditorResponse.Report(() => throw new InvalidOperationException(SensitiveMessage), _ => throw new IOException(SensitiveMessage));
            ApplicationEditorResponse.Report(() => SensitiveMessage, _ => throw new IOException(SensitiveMessage));
            throw primary;
        };
        var observed = Record.Exception(action);
        Assert.True(ReferenceEquals(primary, observed));
    }

    /// <summary>Checks independent complete expectations for both primitive state extremes, including successful diagnostic publication.</summary>
    private static void RequireResponseDiagnostic(PlaywrightException error, string category)
    {
        const string Prefix = "application-editor response-body category=";
        const string Inactive = " page-closed=False browser-connected=False expected-location=False service-worker=False redirected=False request-failed=False";
        const string Active = " page-closed=True browser-connected=True expected-location=True service-worker=True redirected=True request-failed=True";
        Assert.True(ApplicationEditorResponse.Describe(error, default) == Prefix + category + Inactive, Missing);
        var expected = Prefix + category + Active;
        var actual = ApplicationEditorResponse.Describe(error, new(true, true, true, true, true, true));
        Assert.True(actual == expected, Missing);
        string? published = null;
        ApplicationEditorResponse.Report(() => actual, value => published = value);
        Assert.True(published == expected, Missing);
        Assert.False(actual.Contains(Canary, StringComparison.Ordinal), Missing);
    }

    private const string ProgressPrefix = "lifecycle-progress-control-";
    private const string ProgressIdentifierFormat = "N";
    private const string ProgressDestination = "owned-directory";

}
