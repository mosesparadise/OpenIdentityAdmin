using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Observes actual gate UPDATE sessions; absent instrumentation never counts as database contention.</summary>
internal sealed class PilotReplayGateProbe : DbCommandInterceptor
{
    private const string Gate = "PilotProtocolGate";
    private const string Update = "UPDATE";
    private const int ObserveSeconds = 2;
    private readonly TaskCompletionSource<int> contender = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int completedSession;
    private int holder;
    private bool observing;

    /// <summary>Attaches only a passive command observer to the production native database pipeline.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Freezes the last actual completed gate acquisition before the competing request starts.</summary>
    internal void Begin()
    {
        holder = Volatile.Read(ref completedSession);
        observing = true;
    }

    /// <summary>Captures the exact contender connection before its native gate command executes.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (observing && Matches(command))
        {
            contender.TrySetResult(NativeLockObservation.Session(command.Connection!));
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Records only successful, transaction-bound acquisitions with exactly one matched singleton.</summary>
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (!observing && result == 1 && Matches(command))
        {
            Volatile.Write(ref completedSession, NativeLockObservation.Session(command.Connection!));
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Stops only between native queries; timeout and query failures remain distinct from a completed response-first observation.</summary>
    internal async Task<bool> ObserveAsync(string provider, string connection, Task stop, CancellationToken cancellationToken)
    {
        var first = await Task.WhenAny(contender.Task, stop).WaitAsync(TimeSpan.FromSeconds(ObserveSeconds), cancellationToken);
        if (first == stop)
        {
            return false;
        }
        var peer = await contender.Task;
        Assert.True(holder > 0 && peer != holder, "The contender has no distinct observed gate owner.");
        return await new NativeLockObservation(provider, connection).ObserveUntilAsync(peer, holder, stop, cancellationToken);
    }

    /// <summary>Limits instrumentation to the reviewed coordination statement, excluding arbitrary native writes.</summary>
    private static bool Matches(DbCommand command) => command.Transaction is not null &&
        command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains(Gate, StringComparison.Ordinal);
}
