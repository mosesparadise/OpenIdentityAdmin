using System.Diagnostics;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Testing;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Owns one actual command process with a hard lifetime, bounded private output and exact process-generation cleanup.</summary>
internal sealed class GovernanceDriver : IAsyncDisposable
{
    private const int LifetimeSeconds = 60;
    private const int DrainSeconds = 10;
    private const int FrameCharacters = 1_024;
    private const int MaximumFrames = 3;
    private const string UnexpectedOutput = "The governance driver produced an unexpected or oversized frame; raw output is withheld.";
    private const string UnexpectedError = "The governance driver wrote to stderr; raw diagnostics are withheld.";
    private const string Phase = "phase";
    private const string Stage = "stage";
    private const string Failure = "failure";
    private const string Outcome = "outcome";
    private const int UnavailableExit = 2;
    private const int UnknownDiagnostic = -1;
    internal const string Ready = "ready";
    internal const string Committed = "committed";
    internal const string Result = "result";
    internal const string Unavailable = "unavailable";
    private readonly Process process = null!;
    private readonly CancellationTokenSource lifetime = new(TimeSpan.FromSeconds(LifetimeSeconds));
    private readonly CancellationTokenRegistration hardStop;
    private readonly Task<int> errorBytes = Task.FromResult(0);
    private readonly DriverProcessGroup group = null!;
    private readonly CancellationTokenSource streamLifetime = new();
    private readonly TaskCompletionSource hardStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly GovernanceDriverControl mode;
    private const string CleanupFailure = "Owned driver process-group cleanup did not complete.";
    private const string InvalidControl = "This operation requires the matching fixed driver control.";
    private const string StartControl = "start\n";
    private const string ExitControl = "exit\n";
    private const string ProbeControl = "alive\n";
    private bool inputClosed;
    private bool disposed;
    private int terminationFailure;
    private int frames;
    private Task<int>? outputRead;
    internal Stream Input => process.StandardInput.BaseStream;
    internal CancellationToken Token => lifetime.Token;

    /// <summary>Starts the real isolated driver by default, establishes dedicated Linux group ownership and retains its original hard lifetime.</summary>
    internal GovernanceDriver(GovernanceDriverControl control = GovernanceDriverControl.None)
    {
        mode = control;
        try
        {
            process = Process.Start(DriverProcessStart.Create(control))!;
            var generation = ProcessTreeEvidence.Read(process.Id)!.Generation;
            group = new DriverProcessGroup(generation);
            hardStop = lifetime.Token.Register(() =>
            {
                Terminate();
                hardStopped.TrySetResult();
            });
            errorBytes = CountErrorsAsync();
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(Token);
            startup.CancelAfter(TimeSpan.FromSeconds(DrainSeconds));
            group.EstablishAsync(startup.Token).GetAwaiter().GetResult();
        }
        catch
        {
            Task.Run(CleanupFailedStartAsync).GetAwaiter().GetResult();
            throw;
        }
    }

    /// <summary>Retains constructor ownership until transfer, tolerating exit races while always disposing acquired resources.</summary>
    private async Task CleanupFailedStartAsync()
    {
        try
        {
            if (process is not null)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
        }
        finally
        {
            if (group is not null)
            {
                await DisposeAsync();
            }
            else
            {
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(DrainSeconds));
                    if (process is not null)
                    {
                        await process.WaitForExitAsync(deadline.Token);
                    }
                }
                finally { ReleaseHandles(); }
            }
        }
    }

    /// <summary>Closes the writer itself exactly once, avoiding later flushes against an independently disposed base stream.</summary>
    internal void CloseInput()
    {
        if (!inputClosed)
        {
            inputClosed = true;
            process.StandardInput.Close();
        }
    }

    /// <summary>Reads at most one fixed-size frame without printing child-controlled text on parser or assertion failure.</summary>
    internal async Task<JsonDocument> ReadFrameAsync(string? expectedPhase = null)
    {
        Assert.True(++frames <= MaximumFrames, UnexpectedOutput);
        var text = await ReadLineAsync();
        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(text);
            Assert.True(document.RootElement.TryGetProperty(Phase, out var phase) &&
                phase.GetString() is Ready or Committed or Result or Unavailable &&
                (expectedPhase is null || phase.GetString() == expectedPhase), UnexpectedOutput);
            group.Capture();
            return document;
        }
        catch
        {
            var stage = document is not null && document.RootElement.TryGetProperty(Stage, out var value) && value.TryGetInt32(out var number) ? number : UnknownDiagnostic;
            var kind = document is not null && document.RootElement.TryGetProperty(Failure, out var type) && type.TryGetInt32(out var identifier) ? identifier : UnknownDiagnostic;
            document?.Dispose();
            Assert.Fail($"{UnexpectedOutput} {stage}:{kind}");
            throw;
        }
    }

    /// <summary>Consumes only valid post-readiness command framing; unavailable completion makes no rollback assertion.</summary>
    internal async Task<GovernanceOutcome?> CompleteCommandAsync()
    {
        using var first = await ReadFrameAsync();
        var phase = first.RootElement.GetProperty(Phase).GetString();
        if (phase == Unavailable)
        {
            await RequireExitAsync(UnavailableExit);
            return null;
        }
        if (phase == Committed)
        {
            using var final = await ReadFrameAsync(Result);
            Assert.True(final.RootElement.TryGetProperty(Outcome, out var outcome) &&
                outcome.GetString() == nameof(GovernanceOutcome.Accepted), UnexpectedOutput);
            await RequireExitAsync(0);
            return GovernanceOutcome.Accepted;
        }
        Assert.True(phase == Result && first.RootElement.TryGetProperty(Outcome, out _), UnexpectedOutput);
        var text = first.RootElement.GetProperty(Outcome).GetString();
        Assert.True(text is nameof(GovernanceOutcome.Denied) or nameof(GovernanceOutcome.NoChange), UnexpectedOutput);
        await RequireExitAsync(0);
        return Enum.Parse<GovernanceOutcome>(text!);
    }

    /// <summary>Requires exit, bounded asynchronous stream exhaustion and every retained group generation to terminate.</summary>
    internal async Task RequireExitAsync(int expected)
    {
        CloseInput();
        await process.WaitForExitAsync(Token);
        Assert.Equal(expected, process.ExitCode);
        using var drain = CancellationTokenSource.CreateLinkedTokenSource(Token);
        drain.CancelAfter(TimeSpan.FromSeconds(DrainSeconds));
        Assert.Equal(0, await errorBytes.WaitAsync(drain.Token));
        await RequireEndAsync(drain.Token);
        await group.RequireTerminatedAsync(drain.Token);
        Assert.Equal(0, Volatile.Read(ref terminationFailure));
    }

    /// <summary>Kills the retained group even after leader exit, then joins every observed member and both inherited streams.</summary>
    internal async Task KillAsync()
    {
        group.Capture();
        Terminate();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(DrainSeconds));
        await process.WaitForExitAsync(deadline.Token);
        await group.RequireTerminatedAsync(deadline.Token);
        Assert.Equal(0, await errorBytes.WaitAsync(deadline.Token));
        await RequireEndAsync(deadline.Token);
        Assert.Equal(0, Volatile.Read(ref terminationFailure));
    }

    /// <summary>Allows EOF waiting to be cancelled even when a child inherited stdout after the leader exited.</summary>
    private async Task RequireEndAsync(CancellationToken token)
    {
        var character = new char[1];
        Assert.Equal(0, await ReadOutputAsync(character.AsMemory(), token));
    }

    /// <summary>Bounds buffered output character-by-character; stdout cannot allocate arbitrary logs in the parent.</summary>
    private async Task<string> ReadLineAsync()
    {
        var text = new StringBuilder();
        var character = new char[1];
        while (text.Length < FrameCharacters)
        {
            Assert.Equal(1, await ReadOutputAsync(character.AsMemory(), Token));
            if (character[0] == '\n')
            {
                return text.ToString();
            }
            text.Append(character[0]);
        }
        Assert.Fail(UnexpectedOutput);
        return string.Empty;
    }

    /// <summary>Drains bounded stderr without retaining its contents and terminates an unexpectedly noisy child.</summary>
    private async Task<int> CountErrorsAsync()
    {
        var buffer = new byte[FrameCharacters];
        var count = await process.StandardError.BaseStream.ReadAsync(buffer, streamLifetime.Token);
        Array.Clear(buffer);
        if (count != 0)
        {
            Terminate();
            Assert.Fail(UnexpectedError);
        }
        return count;
    }

    /// <summary>Never lets the timer callback throw; cleanup later reports any inability to verify or terminate the retained group.</summary>
    private void Terminate()
    {
        try
        {
            group.Kill();
        }
        catch (Exception) { Interlocked.Exchange(ref terminationFailure, 1); }
    }

    /// <summary>Starts only fixed synthetic descendants after setsid ownership has been established.</summary>
    internal async Task StartControlAsync()
    {
        Assert.True(mode is GovernanceDriverControl.LeaderExit or GovernanceDriverControl.Stubborn, InvalidControl);
        await WriteControlAsync(StartControl);
        using var ready = await ReadFrameAsync(Ready);
        group.RequireDescendant();
    }

    /// <summary>Releases a known leader only after its pipe-holding descendant was captured under exact group ownership.</summary>
    internal async Task ReleaseControlLeaderAsync()
    {
        Assert.True(mode == GovernanceDriverControl.LeaderExit, InvalidControl);
        group.RequireDescendant();
        await WriteControlAsync(ExitControl);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(DrainSeconds));
        await process.WaitForExitAsync(deadline.Token);
        Assert.Equal(0, process.ExitCode);
        group.RequireDescendant();
    }

    /// <summary>Signals the actual stubborn group and requires a subsequent child protocol reply plus a still-live descendant.</summary>
    internal async Task RequireStubbornAsync()
    {
        Assert.True(mode == GovernanceDriverControl.Stubborn, InvalidControl);
        group.TerminateControl();
        await WriteControlAsync(ProbeControl);
        using var ready = await ReadFrameAsync(Ready);
        group.RequireDescendant();
    }

    /// <summary>Observes the unchanged sixty-second parent hard stop and joins the killed group under the existing cleanup ceiling.</summary>
    internal async Task RequireHardStopAsync()
    {
        Assert.True(mode == GovernanceDriverControl.Stubborn, InvalidControl);
        await hardStopped.Task.WaitAsync(TimeSpan.FromSeconds(LifetimeSeconds + DrainSeconds), TestContext.Current.CancellationToken);
        Assert.True(Token.IsCancellationRequested);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(DrainSeconds));
        await process.WaitForExitAsync(deadline.Token);
        await group.RequireTerminatedAsync(deadline.Token);
        Assert.Equal(0, await errorBytes.WaitAsync(deadline.Token));
        await RequireEndAsync(deadline.Token);
        Assert.Equal(0, Volatile.Read(ref terminationFailure));
    }

    /// <summary>Checks retained group evidence after disposal without relying on a disposed Process object.</summary>
    internal void RequireAllTerminated() => Assert.True(group.AllTerminated(), CleanupFailure);

    /// <summary>Sends only a fixed synthetic control command; no arbitrary program or query can enter this seam.</summary>
    private async Task WriteControlAsync(string command)
    {
        var bytes = Encoding.ASCII.GetBytes(command);
        await Input.WriteAsync(bytes, Token);
        await Input.FlushAsync(Token);
    }

    /// <summary>Always attempts group termination, process/stream joins and bounded output draining before releasing owned handles.</summary>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        var failed = false;
        Terminate();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(DrainSeconds));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (Exception) { failed = true; }
        try
        {
            await group.RequireTerminatedAsync(deadline.Token);
        }
        catch (Exception) { failed = true; }
        try
        {
            _ = await errorBytes.WaitAsync(deadline.Token);
        }
        catch (Exception) { failed = true; }
        try
        {
            await JoinOutputAsync(deadline.Token);
        }
        catch (Exception) { failed = true; }
        try
        {
            await DrainRemainingOutputAsync(deadline.Token);
        }
        catch (Exception) { failed = true; }
        streamLifetime.Cancel();
        ObserveFault(errorBytes);
        if (outputRead is not null)
        {
            ObserveFault(outputRead);
        }
        ReleaseHandles();
        Assert.False(failed || Volatile.Read(ref terminationFailure) != 0, CleanupFailure);
    }

    /// <summary>Releases every acquired handle even when startup failed before process or group initialization.</summary>
    private void ReleaseHandles()
    {
        hardStop.Dispose();
        lifetime.Dispose();
        streamLifetime.Dispose();
        process?.Dispose();
    }

    /// <summary>Bounds the caller even if a platform pipe ignores cancellation, while retaining the underlying read for cleanup ownership.</summary>
    private async Task<int> ReadOutputAsync(Memory<char> buffer, CancellationToken token)
    {
        Assert.True(outputRead is null || outputRead.IsCompleted, UnexpectedOutput);
        var reading = process.StandardOutput.ReadAsync(buffer, token).AsTask();
        outputRead = reading;
        try
        {
            return await reading.WaitAsync(token);
        }
        finally { if (reading.IsCompleted) { outputRead = null; } }
    }

    /// <summary>Joins the earlier timed-out read after group termination before another read can touch the same StreamReader.</summary>
    private async Task JoinOutputAsync(CancellationToken token)
    {
        var reading = outputRead;
        if (reading is null)
        {
            return;
        }
        try
        {
            _ = await reading.WaitAsync(token);
        }
        catch (OperationCanceledException) when (reading.IsCanceled) { }
        finally { if (reading.IsCompleted) { outputRead = null; } }
    }

    /// <summary>Observes any eventual stream fault after a reported failed join without presenting it as completed cleanup.</summary>
    private static void ObserveFault(Task task) => _ = task.ContinueWith(static completed => _ = completed.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    /// <summary>Discards at most the protocol's entire bounded remainder during failed-test cleanup without retaining or printing it.</summary>
    private async Task DrainRemainingOutputAsync(CancellationToken token)
    {
        var buffer = new char[FrameCharacters];
        var total = 0;
        int count;
        while ((count = await ReadOutputAsync(buffer.AsMemory(), token)) != 0)
        {
            total = checked(total + count);
            Array.Clear(buffer);
            Assert.True(total <= FrameCharacters * MaximumFrames, UnexpectedOutput);
        }
    }
}
