using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Reports bounded code locations and closed error categories without retaining browser or process diagnostics.</summary>
internal static class LocalDemoFailureEvidence
{
    private const int MaximumErrors = 8;
    private const int MaximumFrames = 8;
    private const int MaximumInspectedFrames = 128;
    private const int MaximumLine = 100000;
    private const string Separator = ",";
    private static readonly CompositeFormat Summary = CompositeFormat.Parse(" Failure category={0}; sources={1}.");
    private static readonly CompositeFormat Location = CompositeFormat.Parse("{0}:{1}");
    private static readonly string[] Sources = ["ApplicationEditorResponse.cs", "ApplicationEditorJourney.cs", "BrowserSecretOracle.cs", "LocalDemoBrowser.cs",
        "ManagedBrowserJourney.cs", "ManagedBrowserRelyingParty.cs", "LocalDemoNavigation.cs"];

    /// <summary>Uses only known source filenames and positive line numbers; bounded inner traversal preserves causal locations without exception text.</summary>
    internal static string Describe(Exception error)
    {
        var pending = new Queue<Exception>([error]);
        var locations = new List<string>();
        for (var inspected = 0; inspected < MaximumErrors && pending.TryDequeue(out var current); inspected++)
        {
            foreach (var frame in new StackTrace(current, true).GetFrames().Take(MaximumInspectedFrames))
            {
                var source = Path.GetFileName(frame.GetFileName());
                var line = frame.GetFileLineNumber();
                if (locations.Count < MaximumFrames && Sources.Contains(source, StringComparer.Ordinal) && line is > 0 and <= MaximumLine)
                {
                    locations.Add(string.Format(CultureInfo.InvariantCulture, Location, source, line));
                }
            }
            IEnumerable<Exception> nested = current is AggregateException aggregate ? aggregate.InnerExceptions : current.InnerException is { } inner ? [inner] : [];
            foreach (var child in nested.Take(MaximumErrors - pending.Count))
            {
                pending.Enqueue(child);
            }
        }
        var category = error switch
        {
            OperationCanceledException => Category.Canceled,
            TimeoutException => Category.Timeout,
            PlaywrightException => Category.Browser,
            Xunit.Sdk.XunitException => Category.Assertion,
            AggregateException => Category.Aggregate,
            _ => Category.Unexpected
        };
        return string.Format(CultureInfo.InvariantCulture, Summary, category, string.Join(Separator, locations));
    }

    private enum Category
    {
        Canceled, Timeout, Browser, Assertion, Aggregate, Unexpected
    }
}
