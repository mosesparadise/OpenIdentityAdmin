using System.Runtime.ExceptionServices;

namespace IdentityPlatform.Testing;

/// <summary>Releases and joins real race operations while preserving the first orchestration failure.</summary>
internal static class NativeRaceCleanup
{
    /// <summary>Never abandons an operation or lets a later cleanup exception replace the original observation failure.</summary>
    internal static async Task CompleteAsync(TaskCompletionSource release, CancellationTokenSource observation,
        Task? waiting, Task winner, Task? peer, ExceptionDispatchInfo? failure)
    {
        release.TrySetResult();
        try
        {
            await observation.CancelAsync();
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        if (waiting is not null)
        {
            try
            {
                await waiting;
            }
            catch (OperationCanceledException) when (observation.IsCancellationRequested) { }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        try
        {
            await Task.WhenAll(winner, peer ?? Task.CompletedTask);
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        failure?.Throw();
    }
}
