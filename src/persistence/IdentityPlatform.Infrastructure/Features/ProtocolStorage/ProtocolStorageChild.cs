using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Suspends a native dispatch while one fresh child completes durable work under the original request budget.</summary>
internal static class ProtocolStorageChild
{
    /// <summary>Retains exclusive parent ownership until the child and its actual cleanup finish, including quarantine.</summary>
    internal static async ValueTask<T> RunAsync<T>(IServiceScopeFactory factory,
        Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> operation)
    {
        var parent = ProtocolStorageAmbient.Current ?? throw ProtocolStorageOwner.Unavailable();
        parent.Request.RequireCurrent();
        using var lease = parent.Scope.Owner.BeginChild();
        ProtocolStorageScope? child = null;
        Task? acquisitionCleanup = null;
        Exception? failure = null;
        T result = default!;
        try
        {
            child = ProtocolStorageScope.Create(factory, parent.Request, cleanup => acquisitionCleanup = cleanup);
            await child.ActivateAsync(parent.Request, parent.Scope.Owner.RequestToken);
            ProtocolStorageAmbient.Current = new(parent.Request, child);
            result = await operation(child, child.Owner.RequestToken);
            child.Owner.RequireCompleted();
            parent.Request.RequireCurrent();
        }
        catch (Exception error) { parent.Request.Fail(); failure = error; }
        finally { ProtocolStorageAmbient.Current = parent; }
        if (child is not null)
        {
            failure = await CleanupAsync(child, parent.Request, failure);
        }
        else if (acquisitionCleanup is not null)
        {
            try
            {
                await acquisitionCleanup;
            }
            catch (Exception cleanup) { failure = new AggregateException(failure!, cleanup); }
        }
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        parent.Request.RequireCurrent();
        return result;
    }

    /// <summary>Preserves primary and cleanup failures without releasing the parent while native resources remain active.</summary>
    private static async Task<Exception?> CleanupAsync(ProtocolStorageScope child, ProtocolStorageRequest request, Exception? failure)
    {
        try
        {
            await child.DisposeAsync();
        }
        catch (Exception cleanup)
        {
            request.Fail();
            failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
        }
        finally { await child.ActualCompletion; }
        return failure;
    }
}
