using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Freezes every persisted deletion field so native/audit side effects cannot silently alter approved intent or terminal evidence.</summary>
internal sealed record CatalogueDeletionSnapshot(Guid Id, string Kind, string TargetId, long TargetRevision, string ActorId,
    string Stamp, long Generation, long FactorGeneration, long EnrollmentRevision, long FactorVerifiedAt, long ApprovalUntil,
    long CreatedAt, long ObservedAt, long Revision, Guid RequestKey, string ScopedKeyHash, string RequestHash, Guid AdmissionAuditId,
    Guid? CompletionAuditId, CatalogueDeletionState State, CatalogueDeletionFailure Failure)
{
    private const string QueuedValue = "queued";
    private const string SucceededValue = "succeeded";
    private const string RejectedValue = "rejected";
    private const string NoneValue = "none";
    private const string StaleValue = "stale";
    private const string ReferencedValue = "referenced";
    private const string AuthorityValue = "authority";
    private const string ExpiredValue = "expired";
    private const string MissingValue = "missing";

    /// <summary>Copies all authority, idempotency and outcome properties without retaining a mutable EF entity.</summary>
    internal static CatalogueDeletionSnapshot Capture(CatalogueDeletion row) => new(row.Id, row.Kind, row.TargetId, row.TargetRevision,
        row.ActorId, row.SecurityStamp, row.AuthenticationGeneration, row.FactorGeneration, row.EnrollmentRevision, row.FactorVerifiedAt,
        row.ApprovalUntil, row.CreatedAt, row.ObservedAt, row.Revision, row.RequestKey, row.ScopedKeyHash, row.RequestHash,
        row.AdmissionAuditId, row.CompletionAuditId, row.State, row.Failure);

    /// <summary>Requires exact independently reloaded state after saving and before the owning operation's only commit attempt.</summary>
    internal async ValueTask VerifyAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        var actual = await ReadAsync(scope, Id, token);
        if (actual is null || this != Capture(actual))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Reads detached durable data; queue visibility alone never grants worker authority or changes tracker state before lock acquisition.</summary>
    internal static ValueTask<CatalogueDeletion?> ReadAsync(ProtocolStorageScope scope, Guid id, CancellationToken token) =>
        scope.Owner.RunAsync(current => new ValueTask<CatalogueDeletion?>(scope.Owner.Database.Set<CatalogueDeletion>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, current)), token);

    /// <summary>Publishes only finite durable status with a separately established current read/mutation authority expiry.</summary>
    internal CatalogueDeletionDetails Details(DateTimeOffset expiry) => new(Id, Kind, TargetId,
        State switch
        {
            CatalogueDeletionState.Queued => QueuedValue,
            CatalogueDeletionState.Succeeded => SucceededValue,
            CatalogueDeletionState.Rejected => RejectedValue,
            _ => throw ProtocolStorageOwner.Unavailable()
        }, Failure switch
        {
            CatalogueDeletionFailure.None => NoneValue,
            CatalogueDeletionFailure.Stale => StaleValue,
            CatalogueDeletionFailure.Referenced => ReferencedValue,
            CatalogueDeletionFailure.Authority => AuthorityValue,
            CatalogueDeletionFailure.Expired => ExpiredValue,
            CatalogueDeletionFailure.Missing => MissingValue,
            _ => throw ProtocolStorageOwner.Unavailable()
        }, DateTimeOffset.FromUnixTimeSeconds(CreatedAt), DateTimeOffset.FromUnixTimeSeconds(ObservedAt), expiry);
}
