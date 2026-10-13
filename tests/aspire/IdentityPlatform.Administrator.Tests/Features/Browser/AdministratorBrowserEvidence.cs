using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Reports source line and allowlisted error categories without retaining secret-bearing browser diagnostics.</summary>
internal static class AdministratorBrowserEvidence
{
    private static readonly string[] SourceFiles = ["EnrollmentBrowserTests.cs", "SecurityBrowserTests.cs", "SecurityBrowserJourney.cs",
        "ClipboardBrowserTests.cs", "AdministratorClipboard.cs"];
    private const string Unknown = "unclassified";
    private static readonly CompositeFormat Failure = CompositeFormat.Parse("Administrator browser failure ({0}); source lines {1}; sensitive diagnostics suppressed.");
    private static readonly string[] Categories = ["ERR_CACHE_MISS", "NS_ERROR_DOCUMENT_NOT_CACHED",
        "Execution context was destroyed", "page is navigating", "Timeout", "NS_BINDING_ABORTED"];

    /// <summary>Runs a secret-bearing journey and limits failures to fixed classifications and repository source line numbers.</summary>
    internal static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            var category = Categories.FirstOrDefault(value => exception.Message.Contains(value, StringComparison.Ordinal)) ?? Unknown;
            var lines = new StackTrace(exception, true).GetFrames()
                .Where(frame => SourceFiles.Contains(Path.GetFileName(frame.GetFileName()), StringComparer.Ordinal)).Select(frame => frame.GetFileLineNumber());
            throw new Xunit.Sdk.XunitException(string.Format(CultureInfo.InvariantCulture, Failure, category, string.Join(',', lines)));
        }
    }
}
