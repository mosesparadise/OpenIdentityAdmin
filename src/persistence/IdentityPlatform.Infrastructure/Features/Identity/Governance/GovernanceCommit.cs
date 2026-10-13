using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Permits one accepted commit only after checking actual effect, locked survival and the exact semantic version advance.</summary>
internal sealed class GovernanceCommit
{
    private readonly GovernanceState state;
    private readonly GovernanceProof proof;
    private readonly long acceptedAt;
    private readonly GovernanceSurvival survival;
    private bool consumed;

    /// <summary>Captures the checked immutable command and proof instead of accepting audit fields or a caller's success assertion.</summary>
    private GovernanceCommit(GovernanceState state, GovernanceProof proof, GovernanceSurvival survival, long acceptedAt)
    {
        this.state = state;
        this.proof = proof;
        this.survival = survival;
        this.acceptedAt = acceptedAt;
    }

    /// <summary>Verifies persisted outcome and survival within the proof's still-live original transaction.</summary>
    internal static async Task<GovernanceCommit> CreateAsync(PlatformDbContext database, CeremonyTransaction transaction,
        GovernanceState state, GovernanceProof proof, GovernanceSurvival survival, CeremonyClock clock)
    {
        transaction.RequireContext(database);
        proof.RequireConsumed(transaction, state.Request, clock.Now);
        await survival.RequireActualAsync(transaction, state);
        var now = clock.Now;
        proof.RequireConsumed(transaction, state.Request, now);
        await RequireRevisionAsync(database, transaction, state);
        return new GovernanceCommit(state, proof, survival, now);
    }

    /// <summary>Consumes accepted commit permission once immediately before inserting its fixed audit intent.</summary>
    internal async Task WriteAsync(PlatformDbContext database, CeremonyTransaction transaction, long now)
    {
        transaction.RequireContext(database);
        proof.RequireConsumed(transaction, state.Request, now);
        if (consumed || now < acceptedAt || database.ChangeTracker.HasChanges())
        {
            throw new FactorUnavailableException();
        }
        await survival.RequireActualAsync(transaction, state);
        await RequireRevisionAsync(database, transaction, state);
        consumed = true;
        GovernanceAudit.Add(database, state.Request, proof.ActorId, true, now);
    }

    /// <summary>Rechecks freshness after audit persistence and immediately before the native commit can expose the transition.</summary>
    internal void RequireFinal(CeremonyTransaction transaction, long now)
    {
        if (!consumed)
        {
            throw new FactorUnavailableException();
        }
        proof.RequireConsumed(transaction, state.Request, now);
    }

    /// <summary>Checks persisted and tracked revisions; technical coordination increments cannot substitute for authority versioning.</summary>
    private static async Task RequireRevisionAsync(PlatformDbContext database, CeremonyTransaction transaction, GovernanceState state)
    {
        var revision = await database.Set<AdministratorGovernance>().AsNoTracking().Where(row => row.Id == GovernanceLimits.Singleton)
            .Select(row => row.AuthorityRevision).SingleAsync(transaction.Token);
        var expected = checked(state.Request.Versions.AuthorityRevision + 1);
        if (revision != expected || state.Authority.AuthorityRevision != expected)
        {
            throw new FactorUnavailableException();
        }
    }
}
