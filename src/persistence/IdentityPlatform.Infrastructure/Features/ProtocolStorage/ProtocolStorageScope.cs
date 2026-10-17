using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Exclusively owns the native service scope, including quarantine when cancellation cannot promptly drain native work.</summary>
internal sealed class ProtocolStorageScope : IAsyncDisposable
{
    private const string CleanupMessage = "Native protocol storage cleanup failed.";
    private readonly AsyncServiceScope scope;
    private readonly ProtocolStorageOwner owner;
    private readonly object sync = new();
    private Task? cleanup;
    private Task? activation;
    private readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task ActualCompletion => completed.Task;
    internal IServiceProvider Services => cleanup is null ? scope.ServiceProvider : throw ProtocolStorageOwner.Unavailable();
    internal ProtocolStorageOwner Owner => owner;
    internal Task Completion => cleanup ?? Task.CompletedTask;

    /// <summary>Retains the entire child scope before its native context or managers begin acquisition.</summary>
    private ProtocolStorageScope(AsyncServiceScope scope, ProtocolStorageOwner owner)
    {
        this.scope = scope;
        this.owner = owner;
    }

    /// <summary>Allocates a tracked child synchronously without blocking on native context activation.</summary>
    internal static ProtocolStorageScope Create(IServiceScopeFactory factory, ProtocolStorageRequest original)
    {
        original.RequireCurrent();
        var child = factory.CreateAsyncScope();
        try
        {
            var result = new ProtocolStorageScope(child, child.ServiceProvider.GetRequiredService<ProtocolStorageOwner>());
            original.Track(result);
            return result;
        }
        catch
        {
            original.Fail();
            var cleanup = child.DisposeAsync().AsTask();
            original.Track(cleanup);
            ProtocolStorageQuarantine.Retain(cleanup);
            throw;
        }
    }

    /// <summary>Activates once at an asynchronous native factory boundary before dispatch or store access.</summary>
    internal Task ActivateAsync(ProtocolStorageRequest? original, CancellationToken token)
    {
        lock (sync)
        {
            if (cleanup is not null)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return activation ??= owner.StartAsync(this, token, original);
        }
    }

    /// <summary>Creates isolated native ownership; callers must use this handle rather than disposing its child scope themselves.</summary>
    internal static async Task<ProtocolStorageScope> OpenAsync(IServiceScopeFactory factory, CancellationToken token, ProtocolStorageRequest? original = null)
    {
        token.ThrowIfCancellationRequested();
        original?.RequireCurrent();
        var child = factory.CreateAsyncScope();
        ProtocolStorageScope? result = null;
        try
        {
            result = new(child, child.ServiceProvider.GetRequiredService<ProtocolStorageOwner>());
            original?.Track(result);
            await result.ActivateAsync(original, token);
            return result;
        }
        catch
        {
            if (result is null)
            {
                await child.DisposeAsync();
            }
            else
            {
                await result.DisposeAsync();
            }
            throw;
        }
    }

    /// <summary>Bounds only the caller's cleanup wait; a timeout retains the same task and never disposes active EF resources.</summary>
    public async ValueTask DisposeAsync()
    {
        if (owner.Dispatches.IsCurrentFlow)
        {
            owner.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        Task task;
        lock (sync)
        {
            if (cleanup is null)
            {
                owner.StopAdmission();
                cleanup = CompleteCleanupAsync();
            }
            task = cleanup;
        }
        try
        {
            await task.WaitAsync(ProtocolStorageLifetime.CleanupLimit, owner.Clock);
        }
        catch (TimeoutException)
        {
            owner.Fail();
            ProtocolStorageQuarantine.Retain(task);
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Signals actual completion only after the full child graph has stopped using its services.</summary>
    private async Task CompleteCleanupAsync()
    {
        try
        {
            await CleanupAsync();
        }
        finally { completed.TrySetResult(); }
    }

    /// <summary>Closes epochs, drains real calls/readers, releases transactional resources, then permits DI context disposal.</summary>
    private async Task CleanupAsync()
    {
        await Task.Yield();
        List<Exception> failures = [];
        try
        {
            await owner.CloseAsync();
        }
        catch (Exception error) { failures.Add(error); }
        try
        {
            if (activation is not null)
            {
                await activation;
            }
        }
        catch { owner.Fail(); }
        try
        {
            await owner.Children.DrainAsync();
            await owner.Dispatches.DrainAsync();
            await owner.DrainAsync();
        }
        catch (Exception error) { failures.Add(error); }
        try
        {
            if (owner.Transaction is not null)
            {
                await owner.Transaction.CleanupAsync();
            }
        }
        catch (Exception error) { failures.Add(error); }
        try
        {
            owner.Dispose();
        }
        catch (Exception error) { failures.Add(error); }
        try
        {
            await scope.DisposeAsync();
        }
        catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0)
        {
            throw new AggregateException(CleanupMessage, failures);
        }
    }
}
