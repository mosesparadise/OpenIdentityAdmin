using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;

/// <summary>Uses finite events and an independent safe correlation; proof selectors and their digests never become audit fields.</summary>
internal static class IssuerContinuationAudit
{
    private const string Started = "administrator-browser-start";
    private const string Attached = "administrator-browser-attach";
    private const string Completed = "administrator-browser-complete";
    private const string Account = "account";
    private const string Anonymous = "anonymous";
    private const string Client = "protocol-client";
    private const string Target = "administrator-browser-login";
    private const string Accepted = "accepted";

    /// <summary>Stages a sanitized start or native attachment audit in the caller's original transaction.</summary>
    internal static void Add(PlatformDbContext database, IssuerContinuation row, bool attached)
    {
        database.Add(new SecurityAuditIntent
        {
            Event = attached ? Attached : Started,
            ActorType = attached ? Client : Anonymous,
            ActorId = attached ? row.ClientId : null,
            TargetType = Target,
            Outcome = Accepted,
            CorrelationId = row.CorrelationId,
            OccurredAt = row.ObservedAt
        });
    }
    /// <summary>Associates the actual newly verified account with the same independent browser-operation correlation.</summary>
    internal static void Complete(PlatformDbContext database, IssuerContinuation row, string userId)
    {
        database.Add(new SecurityAuditIntent
        {
            Event = Completed,
            ActorType = Account,
            ActorId = userId,
            TargetType = Target,
            Outcome = Accepted,
            CorrelationId = row.CorrelationId,
            OccurredAt = row.ObservedAt
        });
    }
}
