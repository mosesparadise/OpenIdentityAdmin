using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Pauses one exact governance context immediately before native commit, after both final survivor checks and audit persistence.</summary>
internal sealed class NativeCommitGate : DbTransactionInterceptor
{
    private const int ObservationSeconds = 10;
    private const string Missing = "The owned governance operation ended before native commit admission.";
    private readonly TaskCompletionSource<int> arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PlatformDbContext? owner;
    internal bool Released
    {
        get; private set;
    }

    /// <summary>Targets only the independently owned governance operation, never fixture setup or its competing security writer.</summary>
    internal void Arm(PlatformDbContext database) => owner = database;

    /// <summary>Bounds observation and rejects early completion without labeling infrastructure failure as an unsafe commit.</summary>
    internal async Task<int> WaitAsync(Task operation, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(ObservationSeconds));
        Assert.True(await Task.WhenAny(arrived.Task, operation).WaitAsync(deadline.Token) == arrived.Task, Missing);
        return await arrived.Task;
    }

    /// <summary>Allows the real provider commit to execute without bypassing any production validation or deadline.</summary>
    internal void Release()
    {
        Released = true;
        release.TrySetResult();
    }

    /// <summary>Captures the actual native blocker only after all production reads, writes and final freshness checks have finished.</summary>
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (owner is not null && ReferenceEquals(owner, eventData.Context))
        {
            Assert.False(arrived.Task.IsCompleted);
            arrived.TrySetResult(await DatabaseWaiters.IdentifyAsync(owner, cancellationToken));
            await release.Task.WaitAsync(cancellationToken);
        }
        return result;
    }
}
