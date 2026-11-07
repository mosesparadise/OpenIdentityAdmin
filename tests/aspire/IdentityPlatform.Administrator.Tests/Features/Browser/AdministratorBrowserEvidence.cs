using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Reports source line and allowlisted error categories without retaining secret-bearing browser diagnostics.</summary>
internal static class AdministratorBrowserEvidence
{
    private static readonly string[] SourceFiles = ["CatalogueBrowserJourney.cs", "CatalogueBrowserTests.cs", "ApplicationEditorResponse.cs", "ApplicationDocumentControls.cs", "ApplicationDocumentLedger.cs", "ApplicationDocumentLifetime.cs", "ApplicationRequestCompletion.cs", "ApplicationRequestCompletionControls.cs", "ApplicationNativeNetwork.cs", "ApplicationNetworkLedger.cs", "ApplicationNetworkControls.cs", "ApplicationLifecycleProgress.cs", "ApplicationLifecycleEvents.cs", "ApplicationNativeWindow.cs", "ApplicationBrowserDiagnostics.cs", "ApplicationNativeVisibility.cs", "EditorRecoveryResponse.cs", "EditorRecoveryWorking.cs", "EditorRecoveryStore.cs", "EditorRecoveryView.cs", "EditorRecoveryBrowserTests.cs", "EditorRecoveryFailures.cs", "EditorRecoveryJourney.cs", "EditorRecoverySends.cs", "ApplicationLifecycleUi.cs", "ApplicationLifecycleBrowserTests.cs", "ApplicationLifecycleObservation.cs", "ApplicationPendingLifecycle.cs", "ApplicationOutputLifecycle.cs", "ApplicationLifecycleWrite.cs", "ApplicationConsentBrowserTests.cs", "ApplicationMutationBrowserTests.cs", "ApplicationManagedBrowserEvidence.cs", "ManagedBrowserJourney.cs", "ManagedBrowserRelyingParty.cs", "ApplicationEditorJourney.cs", "ApplicationEditorBrowserTests.cs", "EnrollmentBrowserTests.cs", "SecurityBrowserTests.cs", "SecurityBrowserJourney.cs",
        "GrantRecoveryIntent.cs", "GrantRecoveryTraffic.cs", "GrantRecoveryView.cs", "GrantRecoveryBrowserStore.cs", "GrantRecoveryBrowserJourney.cs", "ApplicationGrantRecoveryBrowserTests.cs", "ApplicationGrantJourney.cs", "ApplicationGrantBrowserEvidence.cs", "ApplicationGrantBrowserTests.cs",
        "ClipboardBrowserTests.cs", "AdministratorClipboard.cs", "SessionLogoutBrowserTests.cs", "BrowserHttpsJourney.cs",
        "SessionLogoutReturnBrowserTests.cs", "SessionLogoutBindingBrowserTests.cs", "SessionLogoutFailureBrowserTests.cs",
        "SessionLogoutRemoteBrowserTests.cs", "BrowserLogoutJourney.cs", "BrowserLostReturn.cs", "SessionLogoutCrossSiteBrowserTests.cs", "CrossSitePostWitness.cs", "CrossSitePostReceipt.cs",
        "SessionLogoutPresentationBrowserTests.cs", "BrowserLogoutPresentation.cs", "SessionLoginBrowserTests.cs",
        "SessionLoginExpiryBrowserTests.cs", "BrowserNavigationPause.cs", "BrowserLoginEffects.cs", "BrowserLoginDeadline.cs",
        "SessionLoginCorrelationBrowserTests.cs", "SessionLoginAuthorityBrowserTests.cs", "SessionLoginIsolationBrowserTests.cs", "BrowserLostCallback.cs", "BrowserOtherAccount.cs", "SessionStatusBrowserTests.cs", "BrowserNativeSecrets.cs", "BrowserPrivateTraffic.cs", "BrowserTrafficRequests.cs", "BrowserTrafficLifetime.cs", "BrowserFlowSecrets.cs", "BrowserFlowControls.cs", "BrowserWitnessControls.cs", "BrowserResponseCapture.cs", "BrowserResponseWitness.cs", "BrowserSecretOracle.cs", "BrowserSecrecyControls.cs", "SessionSecrecyBrowserTests.cs", "SessionLoginPresentationBrowserTests.cs", "BrowserLoginPresentation.cs", "SessionOriginBrowserTests.cs", "SessionHistoryBrowserTests.cs", "SessionCredentialBrowserTests.cs", "WorkspaceBrowserTests.cs", "WorkspacePresentation.cs", "ApplicationListBrowserTests.cs", "ApplicationApiBrowserTests.cs", "ApplicationAuthorityBrowserTests.cs", "ApplicationProxyBrowserTests.cs", "ApplicationPresentationBrowserTests.cs", "ApplicationDeadlineBrowserTests.cs", "ApplicationProxyCancellation.cs"];
    private const string Unknown = "unclassified";
    private const string Timeout = "Timeout";
    private static readonly CompositeFormat Failure = CompositeFormat.Parse("Administrator browser failure ({0}); source lines {1}; sensitive diagnostics suppressed.");
    private static readonly string[] Categories = ["ERR_CACHE_MISS", "NS_ERROR_DOCUMENT_NOT_CACHED",
        "Execution context was destroyed", "page is navigating", Timeout, "NS_BINDING_ABORTED", "ERR_ABORTED"];

    /// <summary>Runs a secret-bearing journey and limits failures to fixed classifications and repository source line numbers.</summary>
    internal static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException ||
            exception is AggregateException aggregate && aggregate.Flatten().InnerExceptions.Any(error => error is PlaywrightException or TimeoutException))
        {
            IEnumerable<Exception> failures = exception is AggregateException combined ? combined.Flatten().InnerExceptions : [exception];
            var category = failures.Any(error => error is TimeoutException) ? Timeout :
                Categories.FirstOrDefault(value => failures.Any(error => error.Message.Contains(value, StringComparison.Ordinal))) ?? Unknown;
            var lines = failures.SelectMany(error => new StackTrace(error, true).GetFrames())
                .Where(frame => SourceFiles.Contains(Path.GetFileName(frame.GetFileName()), StringComparer.Ordinal)).Select(frame => frame.GetFileLineNumber());
            throw new Xunit.Sdk.XunitException(string.Format(CultureInfo.InvariantCulture, Failure, category, string.Join(',', lines)));
        }
    }
}
