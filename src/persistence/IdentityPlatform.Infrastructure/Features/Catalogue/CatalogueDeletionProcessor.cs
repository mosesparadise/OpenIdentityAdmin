using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Processes one durable deletion under governance/account serialization without retaining human credentials or renewing approval.</summary>
internal static class CatalogueDeletionProcessor
{
    /// <summary>Owns one attempt through actual cleanup; storage/commit ambiguity leaves durable status authoritative and never reports success speculatively.</summary>
    internal static async Task<bool> ExecuteAsync(IServiceScopeFactory factory, Guid jobId, TimeProvider clock, CancellationToken token)
    {
        var request = new ProtocolStorageRequest(clock, token);
        try
        {
            return await request.RunAsync(factory, (scope, current) => OwnedAsync(scope, jobId, current));
        }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Reads candidate identity without tracking, acquires ordered locks, then rechecks the exact durable approval before processing.</summary>
    private static async ValueTask<bool> OwnedAsync(ProtocolStorageScope scope, Guid id, CancellationToken token)
    {
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var candidate = await CatalogueDeletionSnapshot.ReadAsync(scope, id, token);
        if (candidate is null || candidate.State != CatalogueDeletionState.Queued)
        {
            return false;
        }
        var ownership = await ProtocolAdmissionOwnership.AcquireAsync(scope.Owner, candidate.ActorId, token);
        var row = await scope.Owner.RunAsync(current => new ValueTask<CatalogueDeletion?>(scope.Owner.Database.Set<CatalogueDeletion>()
            .SingleOrDefaultAsync(value => value.Id == id, current)), token);
        if (row is null || row.State != CatalogueDeletionState.Queued || CatalogueDeletionSnapshot.Capture(candidate) != CatalogueDeletionSnapshot.Capture(row))
        {
            return false;
        }
        var original = CatalogueDeletionSnapshot.Capture(row);
        var related = await CatalogueDeletionAudit.AdmissionCorrelationAsync(scope, row, token);
        var decision = await DecideAsync(scope, ownership, original, token);
        if (decision.Target is not null)
        {
            await decision.Target.DeleteAsync(scope, token);
        }
        await RequireOriginalAsync(scope, row, original, token);
        var expected = await CompleteAsync(scope, row, decision.Failure, related, token);
        if (decision.Target is not null)
        {
            await decision.Target.RequireAbsentAsync(scope, token);
            if (!await CatalogueDeletionActor.AllowsAsync(scope, ownership, original, token))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
        }
        await expected.VerifyAsync(scope, token);
        if (decision.Target is not null && !CatalogueDeletionActor.CurrentTime(scope.Owner.Clock, original))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await transaction.CommitAsync(token);
        return true;
    }

    /// <summary>Rejects native/save side effects on both tracked and persisted original approval before staging any terminal transition.</summary>
    private static async Task RequireOriginalAsync(ProtocolStorageScope scope, CatalogueDeletion row,
        CatalogueDeletionSnapshot original, CancellationToken token)
    {
        if (CatalogueDeletionSnapshot.Capture(row) != original)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await original.VerifyAsync(scope, token);
    }

    /// <summary>Separates authority to delete from permission to record a safe terminal rejection after authority or target loss.</summary>
    private static async Task<Decision> DecideAsync(ProtocolStorageScope scope, ProtocolAdmissionOwnership ownership,
        CatalogueDeletionSnapshot job, CancellationToken token)
    {
        if (!CatalogueDeletionActor.CurrentTime(scope.Owner.Clock, job))
        {
            return new(null, CatalogueDeletionFailure.Expired);
        }
        if (!await CatalogueDeletionActor.AllowsAsync(scope, ownership, job, token))
        {
            return new(null, CatalogueDeletionFailure.Authority);
        }
        try
        {
            var target = await CatalogueDeleteTarget.ReadAsync(scope, job.Kind, job.TargetId, token);
            if (target.Revision != job.TargetRevision)
            {
                return new(null, CatalogueDeletionFailure.Stale);
            }
            return await target.ReferencedAsync(scope, token)
                ? new(null, CatalogueDeletionFailure.Referenced) : new(target, CatalogueDeletionFailure.None);
        }
        catch (CatalogueRejected failure) when (failure.Failure is CatalogueFailure.Missing or CatalogueFailure.Unsupported)
        {
            return new(null, failure.Failure == CatalogueFailure.Missing ? CatalogueDeletionFailure.Missing : CatalogueDeletionFailure.Stale);
        }
    }

    /// <summary>Stages exactly one terminal audit and revision without lowering the durable observation watermark during clock rollback.</summary>
    private static async ValueTask<CatalogueDeletionSnapshot> CompleteAsync(ProtocolStorageScope scope, CatalogueDeletion row,
        CatalogueDeletionFailure failure, Guid related, CancellationToken token)
    {
        row.State = failure == CatalogueDeletionFailure.None ? CatalogueDeletionState.Succeeded : CatalogueDeletionState.Rejected;
        row.Failure = failure;
        row.ObservedAt = Math.Max(row.ObservedAt, scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds());
        row.Revision = checked(row.Revision + 1);
        var audit = CatalogueDeletionAudit.Create(row, Guid.NewGuid(), related, terminal: true);
        row.CompletionAuditId = audit.Id;
        var expected = CatalogueDeletionSnapshot.Capture(row);
        await scope.Owner.RunAsync(async current =>
        {
            scope.Owner.Database.Add(audit.Entity());
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        await audit.VerifyAsync(scope, token);
        return expected;
    }

    /// <summary>Only a successful decision retains a target capable of deletion; terminal denials cannot fall through to native mutation.</summary>
    private sealed record Decision(CatalogueDeleteTarget? Target, CatalogueDeletionFailure Failure);
}
