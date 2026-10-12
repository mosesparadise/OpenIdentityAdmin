using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Holds scope-local material selection; it is not proof of ceremony eligibility, replay or audit acceptance.</summary>
internal sealed class FactorAccess(PlatformDbContext database)
{
    internal AdministratorInteraction? Staged
    {
        get; private set;
    }
    internal VerifiedStagedFactor? Installation
    {
        get; private set;
    }
    internal AdministratorEnrollment? Recovery
    {
        get; private set;
    }
    internal CancellationToken Token
    {
        get; private set;
    }
    private Guid? transaction;
    private string? selectionBinding;

    /// <summary>Selects a persisted interaction only within its caller-owned transaction.</summary>
    internal IDisposable Select(AdministratorInteraction interaction, CancellationToken token)
    {
        RequireTracked(interaction);
        var lease = Enter(token);
        Staged = interaction;
        selectionBinding = FactorBinding.Digest(interaction);
        return lease;
    }

    /// <summary>Selects recovery material using tracked authoritative enrollment, never a request generation.</summary>
    internal IDisposable SelectRecovery(AdministratorEnrollment enrollment, CancellationToken token)
    {
        RequireTracked(enrollment);
        var lease = Enter(token);
        Recovery = enrollment;
        selectionBinding = FactorBinding.Digest(enrollment);
        return lease;
    }

    /// <summary>Temporarily authorizes exactly one already-verified seed installation.</summary>
    internal IDisposable Install(VerifiedStagedFactor proof, CancellationToken token)
    {
        proof.RequireCurrent(database);
        var lease = Enter(token);
        Installation = proof;
        return lease;
    }

    /// <summary>Rejects use after transaction replacement, disposal or cancellation.</summary>
    internal void RequireCurrent()
    {
        Token.ThrowIfCancellationRequested();
        if (Staged is not null)
        {
            RequireTracked(Staged);
        }
        if (Recovery is not null)
        {
            RequireTracked(Recovery);
        }
        if (transaction is null || database.Database.CurrentTransaction?.TransactionId != transaction ||
            (Staged is not null && FactorBinding.Digest(Staged) != selectionBinding) ||
            (Recovery is not null && FactorBinding.Digest(Recovery) != selectionBinding))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Rejects nested selection and installs a cancellation/transaction binding until disposal.</summary>
    private Lease Enter(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (transaction is not null || database.Database.CurrentTransaction is not { } current)
        {
            throw new FactorUnavailableException();
        }
        transaction = current.TransactionId;
        Token = token;
        return new Lease(this);
    }

    /// <summary>Prevents detached request-shaped objects from selecting material.</summary>
    private void RequireTracked(object value)
    {
        if (database.Entry(value).State != EntityState.Unchanged)
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Clears every selection even when native verification or persistence throws.</summary>
    private sealed class Lease(FactorAccess owner) : IDisposable
    {
        private bool disposed;
        /// <summary>Restores default active-factor selection once, without retaining cancellation or proof state.</summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            owner.Staged = null;
            owner.Installation = null;
            owner.Recovery = null;
            owner.Token = CancellationToken.None;
            owner.transaction = null;
            owner.selectionBinding = null;
        }
    }
}
