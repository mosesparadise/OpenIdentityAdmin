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
    private const int AmbiguousBrowserMatches = 2;
    private const string Separator = ",";
    private static readonly CompositeFormat Summary = CompositeFormat.Parse(" Failure category={0}; sources={1}; browser={2}.");
    private static readonly CompositeFormat Location = CompositeFormat.Parse("{0}:{1}");
    private static readonly string[] Sources = ["ApplicationEditorResponse.cs", "ApplicationEditorJourney.cs", "BrowserSecretOracle.cs", "LocalDemoBrowser.cs",
        "ManagedBrowserJourney.cs", "ManagedBrowserRelyingParty.cs", "LocalDemoNavigation.cs", "ApplicationGrantJourney.cs"];

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
        return string.Format(CultureInfo.InvariantCulture, Summary, category, string.Join(Separator, locations), BrowserFailure(error));
    }

    /// <summary>Maps only closed native Chromium failure identifiers; unknown messages, URLs, form values and call logs are never returned.</summary>
    private static BrowserCategory BrowserFailure(Exception error)
    {
        if (error is not PlaywrightException)
        {
            return BrowserCategory.NotBrowser;
        }
        var matched = Enum.GetValues<BrowserCategory>().Where(value => value is not (BrowserCategory.NotBrowser or BrowserCategory.Unknown) &&
            HasBrowserCode(error.Message, value)).Take(AmbiguousBrowserMatches).ToArray();
        return matched is [var match] ? match : BrowserCategory.Unknown;
    }

    /// <summary>Requires a complete native identifier, rejecting unknown extensions without retaining any following URL or private text.</summary>
    private static bool HasBrowserCode(string message, BrowserCategory category)
    {
        const string Prefix = "net::";
        var marker = Prefix + category;
        var start = message.IndexOf(marker, StringComparison.Ordinal);
        var end = start + marker.Length;
        return start >= 0 && (end == message.Length || char.IsWhiteSpace(message[end]));
    }

    private enum BrowserCategory
    {
        NotBrowser, Unknown, ERR_ABORTED, ERR_EMPTY_RESPONSE, ERR_CONNECTION_REFUSED, ERR_CONNECTION_RESET,
        ERR_CONNECTION_CLOSED, ERR_TIMED_OUT, ERR_NAME_NOT_RESOLVED, ERR_SSL_PROTOCOL_ERROR,
        ERR_CERT_AUTHORITY_INVALID, ERR_CERT_DATE_INVALID, ERR_CERT_COMMON_NAME_INVALID,
        ERR_HTTP_RESPONSE_CODE_FAILURE, ERR_HTTP2_PROTOCOL_ERROR
    }

    private enum Category
    {
        Canceled, Timeout, Browser, Assertion, Aggregate, Unexpected
    }
}
