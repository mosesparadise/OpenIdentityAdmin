using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Retains one original request budget across sequential fresh native scopes without owning shared cancellation resources.</summary>
internal sealed class ProtocolStorageRequest(TimeProvider clock, CancellationToken token)
{
    private readonly long started = clock.GetTimestamp();
    private int active;
    private int faulted;
    internal TimeProvider Clock => clock;
    internal CancellationToken Token => token;
    internal TimeSpan Remaining => ProtocolStorageLifetime.RequestLimit - clock.GetElapsedTime(started);

    /// <summary>Checks the absolute original budget; a child cannot renew it or recover a failed request.</summary>
    internal void RequireCurrent()
    {
        if (Volatile.Read(ref faulted) != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        token.ThrowIfCancellationRequested();
        if (Remaining <= TimeSpan.Zero)
        {
            throw new OperationCanceledException(token);
        }
    }

    /// <summary>Runs one whole native flow using only its owned child services; actual completion and cleanup precede another scope.</summary>
    internal async Task<T> RunAsync<T>(IServiceScopeFactory factory, Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> operation)
    {
        if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
        {
            Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        try
        {
            RequireCurrent();
            var result = await ExecuteAsync(factory, operation);
            RequireCurrent();
            return result;
        }
        catch { Fail(); throw; }
        finally { Volatile.Write(ref active, 0); }
    }

    /// <summary>Preserves both primary and cleanup failures while retaining the existing scope quarantine behavior.</summary>
    private async Task<T> ExecuteAsync<T>(IServiceScopeFactory factory, Func<ProtocolStorageScope, CancellationToken, ValueTask<T>> operation)
    {
        var scope = await ProtocolStorageScope.OpenAsync(factory, token, this);
        Exception? failure = null;
        T result = default!;
        try
        {
            result = await operation(scope, scope.Owner.RequestToken);
            scope.Owner.RequireCompleted();
            RequireCurrent();
        }
        catch (Exception error) { failure = error; }
        try
        {
            await scope.DisposeAsync();
        }
        catch (Exception cleanup)
        {
            failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
        }
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return result;
    }

    /// <summary>Prevents any later child from treating a swallowed owner failure as a fresh request.</summary>
    internal void Fail() => Interlocked.Exchange(ref faulted, 1);
}
