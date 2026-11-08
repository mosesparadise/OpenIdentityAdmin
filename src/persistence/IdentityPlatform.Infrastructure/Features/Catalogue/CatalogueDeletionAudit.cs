using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Defines exact admission and terminal deletion evidence without embedding approval material, native errors or personal profile data.</summary>
internal sealed record CatalogueDeletionAudit(Guid Id, string Event, string ActorId, string TargetType, string TargetId,
    string Outcome, Guid Correlation, Guid? Related, long At)
{
    private const string User = "User";
    private const string Audience = "Audience";
    private const string Scope = "Scope";
    private const string Admitted = "CatalogueDeletionAdmitted";
    private const string Deleted = "CatalogueDeleted";
    private const string Denied = "CatalogueDeletionRejected";
    private const string Accepted = "Accepted";
    private const string Rejected = "Rejected";

    /// <summary>Builds one immutable event from a job already staged under its caller's authority and exact operation correlation.</summary>
    internal static CatalogueDeletionAudit Create(CatalogueDeletion row, Guid correlation, Guid? related, bool terminal) =>
        new(Guid.NewGuid(), terminal ? row.State == CatalogueDeletionState.Succeeded ? Deleted : Denied : Admitted,
            row.ActorId, row.Kind == CatalogueVersion.Scope ? Scope : Audience, row.TargetId,
            terminal && row.State != CatalogueDeletionState.Succeeded ? Rejected : Accepted, correlation, related, row.ObservedAt);

    /// <summary>Stages a finite native audit entity; the owning job transition retains sole save/commit responsibility.</summary>
    internal SecurityAuditIntent Entity() => new()
    {
        Id = Id,
        Event = Event,
        ActorType = User,
        ActorId = ActorId,
        TargetType = TargetType,
        TargetId = TargetId,
        Outcome = Outcome,
        CorrelationId = Correlation,
        RelatedCorrelationId = Related,
        OccurredAt = At
    };

    /// <summary>Verifies every persisted audit field after saving; neither a tracked row nor a row count constitutes atomic evidence.</summary>
    internal ValueTask VerifyAsync(ProtocolStorageScope scope, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var row = await scope.Owner.Database.Set<SecurityAuditIntent>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == Id, current);
        if (row is null || row.Event != Event || row.ActorType != User || row.ActorId != ActorId || row.TargetType != TargetType ||
            row.TargetId != TargetId || row.Outcome != Outcome || row.CorrelationId != Correlation || row.RelatedCorrelationId != Related || row.OccurredAt != At)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }, token);

    /// <summary>Requires the retained admission event to match the durable approved actor/target/time before a worker can process it.</summary>
    internal static ValueTask<Guid> AdmissionCorrelationAsync(ProtocolStorageScope scope, CatalogueDeletion job, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            var row = await scope.Owner.Database.Set<SecurityAuditIntent>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == job.AdmissionAuditId, current);
            if (row is null || row.Event != Admitted || row.ActorType != User || row.ActorId != job.ActorId ||
                row.TargetType != (job.Kind == CatalogueVersion.Scope ? Scope : Audience) || row.TargetId != job.TargetId ||
                row.Outcome != Accepted || row.OccurredAt != job.CreatedAt || row.CorrelationId == Guid.Empty || row.RelatedCorrelationId is not null)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return row.CorrelationId;
        }, token);
}
