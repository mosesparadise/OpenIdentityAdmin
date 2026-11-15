using System.Text.Json;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Freezes original terminal approval/history while allowing the linked job's progress and effective continuation approval to advance.</summary>
internal sealed class ApplicationRetirementBinding
{
    internal const string Event = "ApplicationRetired";
    private const string Actor = "User";
    private const string Target = "Application";
    private const string Accepted = "Accepted";
    private readonly Rows original;
    private readonly string expected;
    internal ApplicationRetirement Receipt => original.Receipt;

    /// <summary>Retains only immutable nonsecret rows and an independent serialized expectation.</summary>
    private ApplicationRetirementBinding(Rows rows)
    {
        original = rows;
        expected = JsonSerializer.Serialize(rows);
    }

    /// <summary>Requires exact persisted terminal policy, original approval and audit links before granting cleanup or replay access.</summary>
    internal static async Task<ApplicationRetirementBinding> ReadAsync(ProtocolStorageScope scope, ApplicationPolicy policy, CancellationToken token)
    {
        var rows = await ReadRowsAsync(scope, policy.ApplicationId, token);
        Validate(rows, policy);
        var result = new ApplicationRetirementBinding(rows);
        await result.JobAsync(scope, token);
        return result;
    }

    /// <summary>Rechecks every immutable scalar after worker/continuation saves without freezing legitimate current progress.</summary>
    internal async Task VerifyAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        if (JsonSerializer.Serialize(original) != expected ||
            JsonSerializer.Serialize(await ReadRowsAsync(scope, Receipt.ApplicationId, token)) != expected)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await JobAsync(scope, token);
    }

    /// <summary>Validates immutable job origin and its current effective approval separately from the original Submit approval.</summary>
    internal async Task<ApplicationGrantJob> JobAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        var pair = await GrantJobAccess.ReadAsync(scope, Receipt.GrantJobId, token);
        if (pair is null || pair.Job.ApplicationId != Receipt.ApplicationId || pair.Job.ApplicationRevision != Receipt.RetiredRevision ||
            pair.Job.CreatedAt != Receipt.RetiredAt || pair.Approval.ApplicationRevision != Receipt.RetiredRevision)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await RequireEffectiveApprovalAsync(scope, pair, token);
        return pair.Job;
    }

    /// <summary>Allows the receipt's original Submit or an independently recorded explicit Continue, never a substituted admission.</summary>
    private ValueTask RequireEffectiveApprovalAsync(ProtocolStorageScope scope, GrantJobPair pair, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var approval = pair.Approval;
        if (approval.Id == Receipt.GrantApprovalId)
        {
            if (JsonSerializer.Serialize(approval) != JsonSerializer.Serialize(original.Approval))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return;
        }
        var database = scope.Owner.Database;
        var audit = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == approval.AuditId, current);
        var detail = await database.Set<ApplicationGrantAudit>().AsNoTracking().SingleOrDefaultAsync(row => row.AuditId == approval.AuditId, current);
        if (approval.Action != GrantApprovalAction.Continue || approval.ApprovedAt < Receipt.RetiredAt ||
            audit is null || audit.Event != GrantAudit.Continued || audit.ActorType != Actor || audit.ActorId != approval.ActorId ||
            audit.TargetType != Target || audit.TargetId != Receipt.ApplicationId || audit.Outcome != Accepted ||
            audit.OccurredAt != approval.ApprovedAt || audit.RelatedCorrelationId is not null ||
            detail is null || detail.JobId != pair.Job.Id || detail.ApprovalId != approval.Id || detail.JobRevision <= 1 ||
            detail.JobRevision > pair.Job.Revision || detail.State != GrantJobState.Queued || detail.Failure != GrantJobFailure.None ||
            detail.AuthorizationCount > pair.Job.AuthorizationCount || detail.TokenCount > pair.Job.TokenCount ||
            detail.AuthorizationDelta != 0 || detail.TokenDelta != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }, token);

    /// <summary>Reads bounded exact primary-key associations inside one execution lease; missing retained history fails closed.</summary>
    private static ValueTask<Rows> ReadRowsAsync(ProtocolStorageScope scope, string id, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var database = scope.Owner.Database;
        var receipt = await database.Set<ApplicationRetirement>().AsNoTracking().SingleOrDefaultAsync(row => row.ApplicationId == id, current)
            ?? throw ProtocolStorageOwner.Unavailable();
        var approval = await database.Set<ApplicationGrantApproval>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == receipt.GrantApprovalId, current)
            ?? throw ProtocolStorageOwner.Unavailable();
        var retirementAudit = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == receipt.AuditId, current)
            ?? throw ProtocolStorageOwner.Unavailable();
        var grantAudit = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == approval.AuditId, current)
            ?? throw ProtocolStorageOwner.Unavailable();
        var detail = await database.Set<ApplicationGrantAudit>().AsNoTracking().SingleOrDefaultAsync(row => row.AuditId == approval.AuditId, current)
            ?? throw ProtocolStorageOwner.Unavailable();
        return new Rows(receipt, approval, retirementAudit, grantAudit, detail);
    }, token);

    /// <summary>Reconstructs original canonical intent and requires terminal revision/time, independent actor and separately scoped hashes.</summary>
    private static void Validate(Rows rows, ApplicationPolicy policy)
    {
        var receipt = rows.Receipt;
        if (!policy.Retired || policy.Enabled || receipt.ApplicationId != policy.ApplicationId || receipt.OriginalRevision <= 0 ||
            receipt.RetiredRevision != policy.Revision || receipt.RetiredRevision - receipt.OriginalRevision != 1 ||
            receipt.RetiredAt != policy.ObservedAt || receipt.RetiredAt < policy.CredentialIssuedAt || receipt.RequestKey == Guid.Empty ||
            receipt.ReplayUntil - receipt.RetiredAt != ApplicationRetirementIntent.ReplaySeconds ||
            !ApplicationEditorRules.IsText(receipt.ActorId, FactorSchema.UserLength) || !Enum.IsDefined(receipt.Reason))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var intent = new ApplicationRetirementIntent(receipt.ApplicationId, ApplicationVersion.Create(receipt.ApplicationId, receipt.OriginalRevision), receipt.RequestKey, receipt.Reason);
        if (receipt.ScopedKeyHash != intent.ScopeHash(receipt.ActorId) || receipt.RequestHash != intent.RequestHash())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        ValidateApproval(rows);
    }

    /// <summary>Requires the original new Submit approval and revision-one zero-progress receipt, never a later effective approval substitution.</summary>
    private static void ValidateApproval(Rows rows)
    {
        var receipt = rows.Receipt;
        var approval = rows.Approval;
        var detail = rows.Detail;
        if (approval.Id != receipt.GrantApprovalId || approval.JobId != receipt.GrantJobId || approval.ApplicationId != receipt.ApplicationId ||
            approval.ActorId != receipt.ActorId || approval.Action != GrantApprovalAction.Submit || approval.Reason != receipt.Reason ||
            approval.RequestKey != receipt.RequestKey || approval.ScopedKeyHash != receipt.ScopedKeyHash || approval.RequestHash != receipt.RequestHash ||
            approval.ApplicationRevision != receipt.RetiredRevision || approval.ApprovedAt != receipt.RetiredAt || approval.FactorVerifiedAt < 0 ||
            approval.FactorVerifiedAt > receipt.RetiredAt || approval.ReplayUntil != receipt.ReplayUntil ||
            detail.JobId != receipt.GrantJobId || detail.ApprovalId != approval.Id || detail.JobRevision != 1 ||
            detail.State != GrantJobState.Queued || detail.Failure != GrantJobFailure.None || detail.AuthorizationCount != 0 || detail.TokenCount != 0 ||
            detail.AuthorizationDelta != 0 || detail.TokenDelta != 0 || rows.RetirementAudit.CorrelationId != rows.GrantAudit.CorrelationId ||
            !Audit(rows.RetirementAudit, receipt, Event) || !Audit(rows.GrantAudit, receipt, GrantAudit.Accepted))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Checks finite actor/target/outcome/correlation chronology without exposing free audit content to response projections.</summary>
    private static bool Audit(SecurityAuditIntent audit, ApplicationRetirement receipt, string action) =>
        audit.Event == action && audit.ActorType == Actor && audit.ActorId == receipt.ActorId && audit.TargetType == Target &&
        audit.TargetId == receipt.ApplicationId && audit.Outcome == Accepted && audit.OccurredAt == receipt.RetiredAt && audit.RelatedCorrelationId is null;

    /// <summary>Groups immutable original rows; current job progress is deliberately verified separately.</summary>
    private sealed record Rows(ApplicationRetirement Receipt, ApplicationGrantApproval Approval, SecurityAuditIntent RetirementAudit,
        SecurityAuditIntent GrantAudit, ApplicationGrantAudit Detail);
}
