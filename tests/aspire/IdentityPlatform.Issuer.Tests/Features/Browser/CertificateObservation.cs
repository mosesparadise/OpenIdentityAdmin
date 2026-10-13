using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

/// <summary>Retains only classified certificate evidence and counters; request values never enter failure diagnostics.</summary>
internal sealed class CertificateObservation
{
    private const int EvidenceSeconds = 2;
    private const int MaximumEvents = 8;
    private const string UnknownState = "unknown";
    private const string MissingCategory = "missing";
    private const string EmptyCategory = "empty";
    private const string RequestChannel = "request";
    private const string NavigationChannel = "navigation";
    private const string EventSeparator = "; ";
    private static readonly CompositeFormat MissingFormat = CompositeFormat.Parse(
        "Native certificate evidence wait expired; navigation={0}; exact failures={1}; unrelated failures={2}; page-closed={3}; browser-connected={4}; dropped={5}; events=[{6}].");
    private static readonly CompositeFormat EventFormat = CompositeFormat.Parse("ordinal={0} elapsed-ms={1} channel={2} exact={3} category={4}");
    private readonly TaskCompletionSource certificate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock gate = new();
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly List<string> events = [];
    private int exactFailures;
    private int unrelatedFailures;
    private int ordinal;
    internal bool HasCertificate => certificate.Task.IsCompletedSuccessfully;

    /// <summary>Accepts only the caller's strict certificate predicate; closed diagnostic classifications never grant acceptance.</summary>
    internal void Observe(bool certificateMatch, bool exactNavigation, string? failure = null)
    {
        lock (gate)
        {
            Record(RequestChannel, exactNavigation, Classify(failure));
            if (exactNavigation)
            {
                ++exactFailures;
                if (certificateMatch)
                {
                    certificate.TrySetResult();
                }
            }
            else
            {
                ++unrelatedFailures;
            }
        }
    }

    /// <summary>Records navigation classification independently of its certificate acceptance and the request-failure channel.</summary>
    internal void ObserveNavigation(PlaywrightException error)
    {
        lock (gate)
        {
            Record(NavigationChannel, exact: true, BrowserEvidence.Classify(error));
        }
    }

    /// <summary>Waits within the original bound, then samples page/browser state without preserving native exception details.</summary>
    internal async Task RequireAsync(PlaywrightException navigationError, Func<bool> pageClosed, Func<bool?> browserConnected)
    {
        try
        {
            await certificate.Task.WaitAsync(TimeSpan.FromSeconds(EvidenceSeconds), TestContext.Current.CancellationToken);
        }
        catch (TimeoutException)
        {
            throw Missing(navigationError, pageClosed(), browserConnected());
        }
    }

    /// <summary>Builds an immutable, bounded report from fixed categories, monotonic event times and current lifecycle flags.</summary>
    internal Xunit.Sdk.XunitException Missing(Exception error, bool pageClosed = false, bool? browserConnected = null)
    {
        lock (gate)
        {
            return new(string.Format(CultureInfo.InvariantCulture, MissingFormat, BrowserEvidence.Classify(error),
                exactFailures, unrelatedFailures, pageClosed, browserConnected?.ToString() ?? UnknownState, Math.Max(0, ordinal - MaximumEvents), string.Join(EventSeparator, events)));
        }
    }

    /// <summary>Maps missing/empty values explicitly and all other text through the existing closed browser classifier.</summary>
    private static string Classify(string? failure) => failure switch
    {
        null => MissingCategory,
        "" => EmptyCategory,
        _ => BrowserEvidence.Classify(new PlaywrightException(failure))
    };

    /// <summary>Records at most eight event receipts under the owner lock; timestamps do not claim native producer ordering.</summary>
    private void Record(string channel, bool exact, string category)
    {
        ++ordinal;
        if (events.Count < MaximumEvents)
        {
            events.Add(string.Format(CultureInfo.InvariantCulture, EventFormat, ordinal,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, channel, exact, category));
        }
    }
}
