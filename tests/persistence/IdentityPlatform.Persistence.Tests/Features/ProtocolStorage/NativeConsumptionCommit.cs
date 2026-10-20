using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Injects a one-shot post-commit observation failure after the actual native provider has committed.</summary>
internal sealed class NativeConsumptionCommit : DbTransactionInterceptor
{
    internal Action? BeforeCommit
    {
        get; set;
    }
    internal Action? AfterCommit
    {
        get; set;
    }

    /// <summary>Injects failure only after all native saves completed but before the actual provider commit.</summary>
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        var action = BeforeCommit;
        BeforeCommit = null;
        action?.Invoke();
        return ValueTask.FromResult(result);
    }

    /// <summary>Consumes the injected action once so an unknown completion cannot masquerade as an automatic retry.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        var action = AfterCommit;
        AfterCommit = null;
        action?.Invoke();
        return Task.CompletedTask;
    }
}
