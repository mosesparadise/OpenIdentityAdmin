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
    internal IServiceProvider Services => cleanup is null ? scope.ServiceProvider : throw ProtocolStorageOwner.Unavailable();
    internal ProtocolStorageOwner Owner => owner;
    internal Task Completion => cleanup ?? Task.CompletedTask;

    /// <summary>Retains the entire child scope before its native context or managers begin acquisition.</summary>
    private ProtocolStorageScope(AsyncServiceScope scope, ProtocolStorageOwner owner)
    {
        this.scope = scope;
        this.owner = owner;
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
            var owner = child.ServiceProvider.GetRequiredService<ProtocolStorageOwner>();
            result = new(child, owner);
            await owner.StartAsync(result, token, original);
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
                cleanup = CleanupAsync();
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
