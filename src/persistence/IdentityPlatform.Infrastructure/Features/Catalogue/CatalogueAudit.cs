using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Freezes exact PII-free audit fields for verification inside the same transaction as native catalogue changes.</summary>
internal sealed record CatalogueAudit(Guid Id, string Event, string TargetType, string TargetId, string ActorId, Guid Correlation, long At)
{
    private const string Actor = "User";
    private const string Accepted = "Accepted";
    private const string Audience = "Audience";
    private const string Scope = "Scope";
    private const string AudienceCreated = "AudienceCreated";
    private const string AudienceEdited = "AudienceEdited";
    private const string ScopeCreated = "ScopeCreated";
    private const string ScopeEdited = "ScopeEdited";

    /// <summary>Persists one finite action with immutable target/correlation metadata; it cannot commit or mint administrator authority.</summary>
    internal static ValueTask<CatalogueAudit> StageAsync(ProtocolStorageScope scope, CatalogueIntent intent, string target,
        string actor, Guid correlation, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var action = (intent.Resource, intent.Action) switch
        {
            (CatalogueResource.Scope, CatalogueAction.Create) => ScopeCreated,
            (CatalogueResource.Scope, CatalogueAction.Edit) => ScopeEdited,
            (CatalogueResource.Audience, CatalogueAction.Create) => AudienceCreated,
            (CatalogueResource.Audience, CatalogueAction.Edit) => AudienceEdited,
            _ => throw ProtocolStorageOwner.Unavailable()
        };
        var stamp = new CatalogueAudit(Guid.NewGuid(), action, intent.Resource == CatalogueResource.Scope ? Scope : Audience,
            target, actor, correlation, scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds());
        scope.Owner.Database.Add(new SecurityAuditIntent
        {
            Id = stamp.Id,
            Event = stamp.Event,
            ActorType = Actor,
            ActorId = actor,
            TargetType = stamp.TargetType,
            TargetId = target,
            Outcome = Accepted,
            CorrelationId = correlation,
            OccurredAt = stamp.At
        });
        await scope.Owner.Database.SaveChangesAsync(current);
        return stamp;
    }, token, write: true);

    /// <summary>Reads fresh committed-intent fields before native commit, rejecting suppression, correlation drift and unexpected related history.</summary>
    internal ValueTask VerifyAsync(ProtocolStorageScope scope, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var row = await scope.Owner.Database.Set<SecurityAuditIntent>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == Id, current);
        if (row is null || row.Event != Event || row.ActorType != Actor || row.ActorId != ActorId || row.TargetType != TargetType ||
            row.TargetId != TargetId || row.Outcome != Accepted || row.CorrelationId != Correlation || row.OccurredAt != At || row.RelatedCorrelationId is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }, token);
}
