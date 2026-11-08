namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Stops a response-first lock observation before later native evidence checks while preserving every observer failure.</summary>
internal static class PilotReplayObservation
{
    /// <summary>Returns the actual first task; a completed replay must signal stop and join its independent observer before verification continues.</summary>
    internal static async Task<Task> SelectAsync(Task replay, Task observation, Task winner, TaskCompletionSource stop)
    {
        var first = await Task.WhenAny(replay, observation, winner);
        if (first == replay)
        {
            stop.TrySetResult();
            await observation;
        }
        return first;
    }

    /// <summary>Calibrates response-first stop/join, exact error propagation and other winners without time-based scheduling or native state changes.</summary>
    internal static async Task RequireOrderingAsync()
    {
        await RequireReplayFirstAsync();
        await RequireFailureAsync(completed: true);
        await RequireFailureAsync(completed: false);
        await RequireOtherFirstAsync(observerFirst: true);
        await RequireOtherFirstAsync(observerFirst: false);
    }

    /// <summary>A completed replay must signal stop synchronously but cannot return until a deliberately pending observer joins.</summary>
    private static async Task RequireReplayFirstAsync()
    {
        var observation = Pending();
        var stop = Pending();
        var replay = Task.CompletedTask;
        var selection = SelectAsync(replay, observation.Task, Pending().Task, stop);
        Assert.True(stop.Task.IsCompletedSuccessfully);
        Assert.False(selection.IsCompleted);
        observation.SetResult();
        Assert.Same(replay, await selection);
    }

    /// <summary>Neither an already-failed observer nor a query failing after the stop signal may be hidden by response-first selection.</summary>
    private static async Task RequireFailureAsync(bool completed)
    {
        var observation = Pending();
        var stop = Pending();
        var error = new InvalidOperationException();
        if (completed)
        {
            observation.SetException(error);
        }
        var selection = SelectAsync(Task.CompletedTask, observation.Task, Pending().Task, stop);
        Assert.True(stop.Task.IsCompletedSuccessfully);
        if (!completed)
        {
            Assert.False(selection.IsCompleted);
            observation.SetException(error);
        }
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(async () => await selection);
        Assert.Same(error, observed);
    }

    /// <summary>A real observer-first or winner-first result cannot be relabelled as replay or stop an otherwise pending observer.</summary>
    private static async Task RequireOtherFirstAsync(bool observerFirst)
    {
        var observation = observerFirst ? Task.CompletedTask : Pending().Task;
        var winner = observerFirst ? Pending().Task : Task.CompletedTask;
        var stop = Pending();
        var selected = await SelectAsync(Pending().Task, observation, winner, stop);
        Assert.Same(observerFirst ? observation : winner, selected);
        Assert.False(stop.Task.IsCompleted);
    }

    /// <summary>Provides a deliberately incomplete calibration task without timers, background work or a cancellation side effect.</summary>
    private static TaskCompletionSource Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
