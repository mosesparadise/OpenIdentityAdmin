using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Retains finite diagnostic classification for historical inspector failures and a protected diagnostic sink.</summary>
internal static class ApplicationEditorResponse
{
    private const int MaximumMessageCharacters = 4096;
    private const string Unavailable = "Response body is unavailable";
    private const string MissingResource = "No resource with given identifier found";
    private const string MissingData = "No data found for resource with given identifier";
    private const string Evicted = "Request content was evicted from inspector cache";
    private static readonly CompositeFormat Summary = CompositeFormat.Parse(
        "application-editor response-body category={0} page-closed={1} browser-connected={2} expected-location={3} service-worker={4} redirected={5} request-failed={6}");

    /// <summary>Diagnostic observation or sink failure cannot replace the original body-read failure.</summary>
    internal static void Report(Func<string> describe, Action<string> write)
    {
        try
        {
            write(describe());
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Maps a bounded message prefix to closed labels and primitive state; raw exception and response values never enter the result.</summary>
    internal static string Describe(PlaywrightException error, ApplicationResponseState state)
    {
        var message = error.Message.AsSpan(0, Math.Min(error.Message.Length, MaximumMessageCharacters));
        var category = message.Contains(Unavailable, StringComparison.Ordinal) ? Category.Unavailable :
            message.Contains(MissingResource, StringComparison.Ordinal) ? Category.MissingResource :
            message.Contains(MissingData, StringComparison.Ordinal) ? Category.MissingData :
            message.Contains(Evicted, StringComparison.Ordinal) ? Category.Evicted : Category.Unknown;
        return string.Format(CultureInfo.InvariantCulture, Summary, category, state.PageClosed, state.BrowserConnected,
            state.ExpectedLocation, state.ServiceWorker, state.Redirected, state.RequestFailed);
    }

    private enum Category
    {
        Unknown, Unavailable, MissingResource, MissingData, Evicted
    }
}

/// <summary>Retains only six booleans about the original response observation, without URLs, headers, bodies or error text.</summary>
internal readonly record struct ApplicationResponseState(bool PageClosed, bool BrowserConnected, bool ExpectedLocation,
    bool ServiceWorker, bool Redirected, bool RequestFailed);
