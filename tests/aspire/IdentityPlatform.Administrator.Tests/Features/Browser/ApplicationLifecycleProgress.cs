using System.Text.Json;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Retains only finite last-entered phases so a killed browser command can report its boundary without private diagnostics.</summary>
internal sealed class ApplicationLifecycleProgress(ApplicationLifecycleScenario scenario, ApplicationPendingBoundary boundary = ApplicationPendingBoundary.Navigation, string? checkpointPath = null)
{
    private const string Variable = "IDENTITY_NATIVE_LIFECYCLE_PROGRESS";
    private const string Temporary = ".tmp";
    private readonly string? path = checkpointPath ?? Environment.GetEnvironmentVariable(Variable);
    private bool unavailable;
    private ApplicationLifecycleStage stage;
    private ApplicationLifecycleDisposal disposal;

    /// <summary>Records an operation phase before the corresponding bounded action.</summary>
    internal void Mark(ApplicationLifecycleStage value)
    {
        stage = value;
        Write();
    }

    /// <summary>Registers a marker immediately after its resource, so reverse disposal records which resource is about to close.</summary>
    internal IDisposable BeforeDispose(ApplicationLifecycleDisposal value) => new Disposal(this, value);

    /// <summary>Atomically replaces one bounded integer tuple; no URLs, credentials, exceptions or arbitrary labels enter the record.</summary>
    private void Write()
    {
        if (path is null || unavailable)
        {
            return;
        }
        try
        {
            var temporary = path + Temporary;
            File.WriteAllText(temporary, JsonSerializer.Serialize(new[] { (int)scenario, (int)boundary, (int)stage, (int)disposal }));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            unavailable = true;
        }
    }

    /// <summary>Records cleanup entry without changing or owning the actual resource's disposal.</summary>
    private sealed class Disposal(ApplicationLifecycleProgress owner, ApplicationLifecycleDisposal value) : IDisposable
    {
        /// <summary>Leaves failures and disposal semantics with the original await-using resource.</summary>
        public void Dispose()
        {
            owner.disposal = value;
            owner.Write();
        }
    }
}

/// <summary>Finite scenarios represented in the dedicated lifecycle lane.</summary>
internal enum ApplicationLifecycleScenario
{
    History, Visibility, Pending
}

/// <summary>Finite operation entry points; these are progress facts, never assertions of completion.</summary>
internal enum ApplicationLifecycleStage
{
    Start, EnvironmentReady, BrowserReady, SignedIn, VisibilityReady, ObserverReady, Prepared,
    OutputReady, Boundary, BoundaryComplete, Release, RequestsJoined, ResultChecked, PrivacyChecked
}

/// <summary>Identifies only which owned resource is about to dispose.</summary>
internal enum ApplicationLifecycleDisposal
{
    None, Observation, Browser, Environment
}
