using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Coordinates explicit cancellation and independently observed HTTPS abort before any cleanup can manufacture the result.</summary>
internal static class NativeClientCancellation
{
    private const int ObservationSeconds = 5;

    /// <summary>Uses a shorter observation bound than the unchanged ten-second native request timeout and always joins client and server.</summary>
    internal static async Task<NativeClientHttpResult> InvokeAsync(NativeIssuerHttpsHost host, NativeClientHttp client, Uri? callback, string? cookie)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pause = new NativeRequestPause(callback is null ? AdministratorProtocolScenario.ParPath : AdministratorProtocolScenario.TokenPath);
        host.Pause = pause;
        var started = Stopwatch.GetTimestamp();
        var operation = client.InvokeAsync(callback, cookie, cancellation.Token);
        ExceptionDispatchInfo? failure = null;
        NativeClientHttpResult? result = null;
        try
        {
            await Task.WhenAny(pause.Reached, operation).WaitAsync(Remaining(started), TestContext.Current.CancellationToken);
            if (!pause.Reached.IsCompletedSuccessfully || operation.IsCompleted)
            {
                await operation;
                throw new InvalidOperationException();
            }
            pause.BeginCancellation();
            await cancellation.CancelAsync();
            await pause.Cancelled.WaitAsync(Remaining(started), TestContext.Current.CancellationToken);
            pause.RequireCancelled();
            result = await operation.WaitAsync(Remaining(started), TestContext.Current.CancellationToken);
            _ = Remaining(started);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            failure = await CompleteAsync(host, pause, cancellation, operation, failure);
        }
        failure?.Throw();
        return result!;
    }

    /// <summary>Does not renew the observation budget at readiness, explicit cancellation or client completion.</summary>
    private static TimeSpan Remaining(long started)
    {
        var remaining = TimeSpan.FromSeconds(ObservationSeconds) - Stopwatch.GetElapsedTime(started);
        return remaining > TimeSpan.Zero ? remaining : throw new TimeoutException();
    }

    /// <summary>Release terminates interception; primary failures survive cancellation, client completion and independent server drain.</summary>
    private static async Task<ExceptionDispatchInfo?> CompleteAsync(NativeIssuerHttpsHost host, NativeRequestPause pause,
        CancellationTokenSource cancellation, Task operation, ExceptionDispatchInfo? failure)
    {
        pause.Release();
        try
        {
            await cancellation.CancelAsync();
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        try
        {
            await operation;
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        try
        {
            await host.DrainAsync(CancellationToken.None);
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        host.Pause = null;
        return failure;
    }
}
