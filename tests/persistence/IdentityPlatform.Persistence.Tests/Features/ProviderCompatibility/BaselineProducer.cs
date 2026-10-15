using System.Diagnostics;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Runs the frozen producer in a separate process so old/new assembly identities cannot mix.</summary>
internal static class BaselineProducer
{
    private const string ExecutableSetting = "PROVIDER_BASELINE_PRODUCER";
    private const string ManifestSetting = "PROVIDER_BASELINE_MANIFEST";
    private const string ProviderSetting = "COMPATIBILITY_PROVIDER";
    private const string ConnectionSetting = "COMPATIBILITY_CONNECTION";
    private const string Dotnet = "dotnet";
    private const int CleanupSeconds = 10;

    /// <summary>Passes disposable connection material only through the child environment and requires successful old-binary population.</summary>
    internal static async Task PopulateAsync(string provider, string connection, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var executable = Environment.GetEnvironmentVariable(ExecutableSetting);
        Assert.True(executable is not null && File.Exists(executable));
        var manifest = Environment.GetEnvironmentVariable(ManifestSetting);
        Assert.True(manifest is not null && File.Exists(manifest));
        BaselineArtifactManifest.Require(executable!, manifest!);
        var start = new ProcessStartInfo(Dotnet) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(executable!);
        start.Environment[ProviderSetting] = provider;
        start.Environment[ConnectionSetting] = connection;
        token.ThrowIfCancellationRequested();
        using var process = Process.Start(start)!;
        Task<string>? output = null;
        Task<string>? error = null;
        Exception? failure = null;
        try
        {
            output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            error = process.StandardError.ReadToEndAsync(CancellationToken.None);
            await process.WaitForExitAsync(token);
            Assert.Equal(0, process.ExitCode);
        }
        catch (Exception exception) { failure = exception; throw; }
        finally
        {
            try
            {
                await StopAndDrainAsync(process, output, error);
            }
            catch when (failure is not null) { /* Preserve the operation failure after bounded cleanup. */ }
        }
    }

    /// <summary>Bounds independent child termination and output ownership even when the operation token was cancelled.</summary>
    private static async Task StopAndDrainAsync(Process process, Task<string>? output, Task<string>? error)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
        await process.WaitForExitAsync(cleanup.Token);
        await Task.WhenAll(output ?? Task.FromResult(string.Empty), error ?? Task.FromResult(string.Empty)).WaitAsync(cleanup.Token);
    }
}
