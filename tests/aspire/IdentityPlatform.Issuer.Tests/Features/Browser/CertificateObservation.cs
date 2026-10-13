using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

/// <summary>Retains only classified certificate evidence and counters; request values never enter failure diagnostics.</summary>
internal sealed class CertificateObservation
{
    private const int EvidenceSeconds = 2;
    private const string MissingEvidence = "Native certificate evidence wait expired; navigation={0}; exact failures={1}; unrelated failures={2}.";
    private static readonly CompositeFormat MissingFormat = CompositeFormat.Parse(MissingEvidence);
    private readonly TaskCompletionSource certificate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int exactFailures;
    private int unrelatedFailures;
    internal bool HasCertificate => certificate.Task.IsCompletedSuccessfully;

    /// <summary>Accepts only the caller's strict certificate predicate; other failures remain diagnostic counts.</summary>
    internal void Observe(bool certificateMatch, bool exactNavigation)
    {
        if (exactNavigation)
        {
            Interlocked.Increment(ref exactFailures);
            if (certificateMatch)
            {
                certificate.TrySetResult();
            }
        }
        else
        {
            Interlocked.Increment(ref unrelatedFailures);
        }
    }

    /// <summary>Waits within the original bound and emits a fixed classification without preserving the raw exception.</summary>
    internal async Task RequireAsync(PlaywrightException navigationError)
    {
        try
        {
            await certificate.Task.WaitAsync(TimeSpan.FromSeconds(EvidenceSeconds), TestContext.Current.CancellationToken);
        }
        catch (TimeoutException)
        {
            throw Missing(navigationError);
        }
    }
    /// <summary>Builds a secret-safe missing-evidence report using only the fixed browser classifier and counters.</summary>
    internal Xunit.Sdk.XunitException Missing(Exception error) => new(
        string.Format(CultureInfo.InvariantCulture, MissingFormat, BrowserEvidence.Classify(error), Volatile.Read(ref exactFailures), Volatile.Read(ref unrelatedFailures)));
}
