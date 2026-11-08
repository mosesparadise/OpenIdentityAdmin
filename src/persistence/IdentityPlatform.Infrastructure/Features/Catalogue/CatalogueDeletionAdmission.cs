using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Stages durable deletion approval and one atomic audit under already acquired current API authority.</summary>
internal static class CatalogueDeletionAdmission
{
    /// <summary>Replays only identical actor/target/version intent without renewing approval or reading a target already deleted by the worker.</summary>
    internal static async Task<CatalogueDeletionAdmissionResult> StageAsync(ProtocolStorageScope scope, CatalogueDeleteIntent intent,
        CatalogueDeletionApproval approval, Guid correlation, CancellationToken token)
    {
        var replay = await ReplayAsync(scope, intent, approval.ActorId, token);
        if (replay is not null)
        {
            await CatalogueDeletionAudit.AdmissionCorrelationAsync(scope, replay, token);
            return new(CatalogueDeletionSnapshot.Capture(replay), false);
        }
        var target = await CatalogueDeleteTarget.ReadAsync(scope, intent.Kind, intent.Id, token);
        target.RequireVersion(intent.Version);
        if (await target.ReferencedAsync(scope, token))
        {
            throw new CatalogueRejected(CatalogueFailure.Referenced);
        }
        var now = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds();
        if (approval.ApprovalUntil <= now || approval.FactorVerifiedAt > now || approval.FactorVerifiedAt < 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var job = Create(intent, approval, target.Revision, now);
        var audit = CatalogueDeletionAudit.Create(job, correlation, null, terminal: false);
        job.AdmissionAuditId = audit.Id;
        var expected = CatalogueDeletionSnapshot.Capture(job);
        await SaveAsync(scope, job, audit, token);
        await expected.VerifyAsync(scope, token);
        await audit.VerifyAsync(scope, token);
        await target.VerifyAsync(scope, token);
        if (await target.ReferencedAsync(scope, token))
        {
            throw new CatalogueRejected(CatalogueFailure.Referenced);
        }
        return new(expected, true);
    }

    /// <summary>Copies only the private authority receipt's original bindings; no request field can choose an actor, generation or deadline.</summary>
    private static CatalogueDeletion Create(CatalogueDeleteIntent intent, CatalogueDeletionApproval approval, long revision, long now) => new()
    {
        Id = Guid.NewGuid(),
        Kind = intent.Kind,
        TargetId = intent.Id,
        TargetRevision = revision,
        ActorId = approval.ActorId,
        SecurityStamp = approval.Stamp,
        AuthenticationGeneration = approval.Generation,
        FactorGeneration = approval.FactorGeneration,
        EnrollmentRevision = approval.EnrollmentRevision,
        FactorVerifiedAt = approval.FactorVerifiedAt,
        ApprovalUntil = approval.ApprovalUntil,
        CreatedAt = now,
        ObservedAt = now,
        RequestKey = intent.RequestKey,
        ScopedKeyHash = intent.ScopeHash(approval.ActorId),
        RequestHash = intent.RequestHash()
    };

    /// <summary>Checks all replay provenance, including the exact retained revision, before returning an existing durable status.</summary>
    private static ValueTask<CatalogueDeletion?> ReplayAsync(ProtocolStorageScope scope, CatalogueDeleteIntent intent, string actor, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            var key = intent.ScopeHash(actor);
            var row = await scope.Owner.Database.Set<CatalogueDeletion>().AsNoTracking().SingleOrDefaultAsync(value => value.ScopedKeyHash == key, current);
            if (row is not null && (row.ActorId != actor || row.Kind != intent.Kind || row.TargetId != intent.Id || row.RequestKey != intent.RequestKey ||
                row.RequestHash != intent.RequestHash() || !CatalogueVersion.Matches(intent.Version, row.Kind, row.TargetId, row.TargetRevision)))
            {
                throw new CatalogueRejected(CatalogueFailure.Duplicate);
            }
            return row;
        }, token);

    /// <summary>Rejects another live job for the same target and stages job/audit in one save under governance serialization.</summary>
    private static ValueTask SaveAsync(ProtocolStorageScope scope, CatalogueDeletion job, CatalogueDeletionAudit audit, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            if (await scope.Owner.Database.Set<CatalogueDeletion>().AnyAsync(row => row.Kind == job.Kind && row.TargetId == job.TargetId && row.State == CatalogueDeletionState.Queued, current))
            {
                throw new CatalogueRejected(CatalogueFailure.Duplicate);
            }
            scope.Owner.Database.AddRange(job, audit.Entity());
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
}

/// <summary>Distinguishes new approval from retained replay so final current authority never rewrites an earlier accepted job.</summary>
internal sealed record CatalogueDeletionAdmissionResult(CatalogueDeletionSnapshot Job, bool Admitted);
