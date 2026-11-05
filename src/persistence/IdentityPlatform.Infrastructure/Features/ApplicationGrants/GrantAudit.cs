using IdentityPlatform.Infrastructure.Features.Identity.Persistence;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Creates finite security intent and exact committed-progress details; the caller saves and verifies both in one transaction.</summary>
internal static class GrantAudit
{
    internal const string Accepted = "ApplicationGrantRevocationAccepted";
    internal const string Continued = "ApplicationGrantRevocationContinued";
    internal const string Advanced = "ApplicationGrantRevocationAdvanced";
    internal const string Succeeded = "ApplicationGrantRevocationSucceeded";
    internal const string Stopped = "ApplicationGrantRevocationStopped";
    private const string Actor = "User";
    private const string Target = "Application";
    private const string Outcome = "Accepted";

    /// <summary>Retains safe actor/target/correlation provenance without credentials, free text or native token identities.</summary>
    internal static SecurityAuditIntent Intent(ApplicationGrantJob job, string actor, Guid correlation, long now, string action) => new()
    {
        Event = action,
        ActorType = Actor,
        ActorId = actor,
        TargetType = Target,
        TargetId = job.ApplicationId,
        Outcome = Outcome,
        CorrelationId = correlation,
        OccurredAt = now
    };

    /// <summary>Freezes the resulting revision, effective approval and exact deltas for independent persisted comparison.</summary>
    internal static ApplicationGrantAudit Detail(ApplicationGrantJob job, Guid auditId, long authorizations, long tokens) => new()
    {
        AuditId = auditId,
        JobId = job.Id,
        ApprovalId = job.EffectiveApprovalId,
        JobRevision = job.Revision,
        AuthorizationDelta = authorizations,
        TokenDelta = tokens,
        AuthorizationCount = job.AuthorizationCount,
        TokenCount = job.TokenCount,
        State = job.State,
        Failure = job.Failure
    };
}
