using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Propagates transaction cancellation to native managers without granting material selection.</summary>
internal sealed class NativeOperation(PlatformDbContext database)
{
    private Guid? transaction;
    private CancellationToken token;

    /// <summary>Rejects managers whose native persistence context differs even if their cancellation token was copied.</summary>
    internal void RequireContext(PlatformDbContext current, CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(database, current) || transaction is null || !cancellationToken.CanBeCanceled || Token != cancellationToken)
        {
            throw new FactorUnavailableException();
        }
    }
    internal CancellationToken Token
    {
        /// <summary>Supplies cancellation only while its original native transaction remains active.</summary>
        get
        {
            if (transaction is not null && database.Database.CurrentTransaction?.TransactionId != transaction)
            {
                throw new FactorUnavailableException();
            }
            return token;
        }
    }

    /// <summary>Binds one operation to the current transaction; nested or detached owners are rejected.</summary>
    internal IDisposable Enter(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (transaction is not null || database.Database.CurrentTransaction is not { } current)
        {
            throw new FactorUnavailableException();
        }
        transaction = current.TransactionId;
        token = cancellationToken;
        return new Lease(this);
    }

    /// <summary>Restores the inert default when the owning transaction leaves scope.</summary>
    private sealed class Lease(NativeOperation owner) : IDisposable
    {
        private bool disposed;
        /// <summary>Clears the owner exactly once, including exceptional exits.</summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            owner.transaction = null;
            owner.token = CancellationToken.None;
        }
    }
}
