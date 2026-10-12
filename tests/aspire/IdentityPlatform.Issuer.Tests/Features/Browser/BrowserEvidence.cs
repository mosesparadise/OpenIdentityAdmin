using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

internal enum BrowserPhase
{
    Journey, Creation, Ready, TrustedIssuer, TrustedRelyingParty, UntrustedCertificate,
    InitialNavigation, FirstTabNavigation, SecondTabNavigation, CredentialEntry, CredentialSubmission, ManualContinuation, Callback, CallbackValidation, StorageInspection
}

internal static class BrowserEvidence
{
    private sealed class PhaseState
    {
        public BrowserPhase Value
        {
            get; set;
        }
    }
    private static readonly AsyncLocal<PhaseState?> Current = new();
    private static readonly string[] ErrorMarkers =
    [
        "SEC_ERROR_UNKNOWN_ISSUER", "SEC_ERROR_UNTRUSTED_ISSUER", "SEC_ERROR_CA_CERT_INVALID",
        "MOZILLA_PKIX_ERROR_SELF_SIGNED_CERT", "ERR_CERT_AUTHORITY_INVALID", "Unacceptable TLS certificate",
        "Target page, context or browser has been closed", "Missing X server", "Executable doesn't exist",
        "SEC_ERROR_UNTRUSTED_CERT", "SEC_ERROR_UNKNOWN", "SSL_ERROR_UNKNOWN",
        // Diagnostic categories only: none expands certificate-rejection acceptance.
        "Provisiolal navigation canceled.", "Load failed", "An SSL error has occurred",
        "Connection terminated unexpectedly", "Could not connect", "Connection refused",
        "cancelled", "canceled", "Timeout"
    ];
    public static string Classify(Exception exception) => ErrorMarkers.FirstOrDefault(value => exception.Message.Contains(value, StringComparison.Ordinal)) ?? "unclassified browser error";

    public static void Mark(BrowserPhase phase)
    {
        if (Current.Value is { } state)
        {
            state.Value = phase;
        }
    }

    public static async Task RunAsync(Func<Task> action)
    {
        var previous = Current.Value;
        var state = new PhaseState { Value = BrowserPhase.Journey };
        Current.Value = state;
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            // Only fixed source-defined classifications escape; never action logs, values or URLs.
            var marker = Classify(exception);
            throw new Xunit.Sdk.XunitException($"Browser phase {state.Value} failed ({marker}); sensitive diagnostics suppressed.");
        }
        finally { Current.Value = previous; }
    }
}
