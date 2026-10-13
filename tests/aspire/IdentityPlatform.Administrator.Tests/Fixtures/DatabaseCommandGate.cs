using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Pauses one actually executed transactional write and reports its native session for independent wait-graph observation.</summary>
internal sealed class DatabaseCommandGate : DbCommandInterceptor
{
    private string? table;
    private string operation = string.Empty;
    private const int ObservationSeconds = 10;
    private const string MissingBoundary = "The owned operation ended before its required transactional write was observed.";
    private TaskCompletionSource<int> arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Arms one positive affected-row boundary; the caller owns release/cancellation and draining of its operation.</summary>
    internal void Arm(string tableName, string commandOperation)
    {
        table = tableName;
        operation = commandOperation;
        arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Waits for actual completed SQL execution, not an elapsed-time approximation.</summary>
    internal async Task<int> WaitAsync(Task operationTask, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(ObservationSeconds));
        var first = await Task.WhenAny(arrived.Task, operationTask).WaitAsync(deadline.Token);
        Assert.True(first == arrived.Task, MissingBoundary);
        return await arrived.Task;
    }

    /// <summary>Releases the observed write so the production transaction may continue to its own checks and commit.</summary>
    internal void Release() => released.TrySetResult();

    /// <summary>Requires affected rows and a live native transaction before pausing after the selected real write.</summary>
    public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (table is not { } marker || !command.CommandText.Contains(marker, StringComparison.Ordinal) ||
            !command.CommandText.Contains(operation, StringComparison.Ordinal))
        {
            return result;
        }
        table = null;
        Assert.True(result > 0);
        var database = Assert.IsType<PlatformDbContext>(eventData.Context);
        Assert.NotNull(database.Database.CurrentTransaction);
        arrived.TrySetResult(await DatabaseWaiters.IdentifyAsync(database, cancellationToken));
        await released.Task.WaitAsync(cancellationToken);
        return result;
    }
}
