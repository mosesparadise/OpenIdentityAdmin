using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Injects a one-shot post-commit observation failure after the actual native provider has committed.</summary>
internal sealed class NativeConsumptionCommit : DbTransactionInterceptor
{
    internal Action? AfterCommit
    {
        get; set;
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
