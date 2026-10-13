using System.Collections.Frozen;
using System.Data.Common;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Coordinates internal governance within one discovery-to-commit deadline; no route or external management authority is exposed.</summary>
internal sealed class GovernanceService(PlatformDbContext database, NativeOperation operation, GovernanceDiscovery discovery,
    GovernanceGrants grants, GovernanceUsability usability, GovernanceSurvival survival, GovernanceMutation mutation,
    UserManager<PlatformUser> users, FactorAccess access, CeremonyBudget budget, CeremonyClock clock, CeremonyReplayLedger ledger)
{
    /// <summary>Returns immutable current observations while rolling back every technical lock and tracking side effect.</summary>
    internal async Task<GovernanceObservation> ReadAsync(string actorId, CancellationToken token)
    {
        using var deadline = CreateDeadline(token);
        try
        {
            return await ReadCurrentAsync(actorId, deadline.Token);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or InvalidOperationException or OverflowException)
        {
            throw new GovernanceUnavailableException();
        }
    }

    /// <summary>Evaluates only current native account authority while holding the complete discovered lock set.</summary>
    private async Task<GovernanceObservation> ReadCurrentAsync(string actorId, CancellationToken token)
    {
        access.RequireActive();
        var plan = await discovery.DiscoverAsync(actorId, null, token);
        await using var transaction = await CeremonyTransaction.OpenGovernanceAsync(database, operation, plan, token);
        await discovery.RequireUnchangedAsync(transaction, actorId, null);
        var view = await grants.ReadAsync(transaction, null);
        var population = new HashSet<string>(StringComparer.Ordinal);
        var permissions = FrozenSet<string>.Empty;
        foreach (var id in plan.Users)
        {
            var account = await CeremonyAccount.LoadAsync(database, transaction, id);
            if (id == actorId && await usability.IsEligibleAsync(transaction, account))
            {
                permissions = view.Permissions(actorId);
            }
            if (await usability.IsUsableAsync(transaction, account, view))
            {
                population.Add(id);
            }
        }
        var revision = await database.Set<AdministratorGovernance>().AsNoTracking().Where(row => row.Id == GovernanceLimits.Singleton)
            .Select(row => row.AuthorityRevision).SingleAsync(transaction.Token);
        return new GovernanceObservation(permissions, population.ToFrozenSet(StringComparer.Ordinal), revision);
    }

    /// <summary>Runs one immutable command; unavailable completion must be reconciled from state/audit and never retried to discover success.</summary>
    internal async Task<GovernanceOutcome> ExecuteAsync(GovernanceRequest request, JourneySessionCandidate candidate,
        string password, string code, CancellationToken token)
    {
        using var deadline = CreateDeadline(token);
        try
        {
            access.RequireActive();
            var plan = await discovery.DiscoverAsync(candidate.UserId, request, deadline.Token);
            await using var transaction = await CeremonyTransaction.OpenGovernanceAsync(database, operation, plan, deadline.Token);
            await discovery.RequireUnchangedAsync(transaction, candidate.UserId, request);
            var state = await GovernanceState.ReadAsync(database, grants, transaction, candidate.UserId, request);
            if (await JourneySessionProof.ReadAsync(database, users, clock, transaction, state.Actor, candidate) is null ||
                !await usability.IsEligibleAsync(transaction, state.Actor))
            {
                return GovernanceOutcome.Denied;
            }
            if (state.IsNoChange())
            {
                return GovernanceOutcome.NoChange;
            }
            await survival.RequireProjectedAsync(transaction, state);
            return await VerifyAndApplyAsync(transaction, state, candidate, password, code);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or InvalidOperationException or OverflowException)
        {
            throw new GovernanceUnavailableException();
        }
    }

    /// <summary>Separates failed native proof from accepted writes, with one typed audit capability per path.</summary>
    private async Task<GovernanceOutcome> VerifyAndApplyAsync(CeremonyTransaction transaction, GovernanceState state,
        JourneySessionCandidate candidate, string password, string code)
    {
        var verification = await GovernanceVerification.VerifyAsync(database, users, access, budget, clock, ledger, usability,
            transaction, state, candidate, password, code);
        if (!verification.Accepted)
        {
            var denial = await GovernanceDenial.RecordAsync(database, transaction, verification, budget, clock);
            await transaction.CommitGovernanceDenialAsync(denial, clock);
            return GovernanceOutcome.Denied;
        }
        state.RequireAuthorized(transaction);
        var proof = GovernanceProof.Create(transaction, verification, clock.Now);
        await budget.CompleteGovernanceAsync(transaction, state.Actor, proof);
        await mutation.ApplyAsync(transaction, state, proof, clock.Now);
        var commit = await GovernanceCommit.CreateAsync(database, transaction, state, proof, survival, clock);
        await transaction.CommitGovernanceAsync(commit, clock);
        return GovernanceOutcome.Accepted;
    }

    /// <summary>Starts the existing ceiling before discovery so nested transaction budgets cannot extend the total operation.</summary>
    private static CancellationTokenSource CreateDeadline(CancellationToken token)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(AdministratorPolicy.OperationSeconds));
        return deadline;
    }
}

/// <summary>Contains read-only observations, never transferable mutation authority.</summary>
internal sealed record GovernanceObservation(FrozenSet<string> Permissions, FrozenSet<string> UsableAdministrators, long AuthorityRevision);

/// <summary>Reports unavailable or indeterminate completion without native diagnostics or credential material.</summary>
internal sealed class GovernanceUnavailableException : InvalidOperationException
{
    private const string SafeMessage = "Administrator governance completion is unavailable; reconcile stored state before further action.";

    /// <summary>Creates a fixed diagnostic without retaining provider exceptions containing sensitive values.</summary>
    internal GovernanceUnavailableException() : base(SafeMessage) { }
}
