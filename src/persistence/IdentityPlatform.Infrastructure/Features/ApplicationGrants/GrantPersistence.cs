using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Freezes safe product rows before save callbacks and compares every scalar against fresh persisted observations.</summary>
internal sealed class GrantPersistence
{
    private readonly Guid jobId;
    private readonly Guid approvalId;
    private readonly Guid auditId;
    private readonly string job;
    private readonly string approval;
    private readonly string audit;
    private readonly string detail;
    private readonly ApplicationGrantJob localJob;
    private readonly ApplicationGrantApproval localApproval;
    private readonly SecurityAuditIntent localAudit;
    private readonly ApplicationGrantAudit localDetail;

    /// <summary>Captures immutable expected contents before native persistence can mutate tracked state; no secrets enter these entities.</summary>
    internal GrantPersistence(ApplicationGrantJob job, ApplicationGrantApproval approval, SecurityAuditIntent audit, ApplicationGrantAudit detail)
    {
        localJob = job;
        localApproval = approval;
        localAudit = audit;
        localDetail = detail;
        jobId = job.Id;
        approvalId = approval.Id;
        auditId = audit.Id;
        this.job = JsonSerializer.Serialize(job);
        this.approval = JsonSerializer.Serialize(approval);
        this.audit = JsonSerializer.Serialize(audit);
        this.detail = JsonSerializer.Serialize(detail);
    }

    /// <summary>Rejects suppressed, altered, deleted or foreign rows rather than trusting tracked entities after SaveChanges.</summary>
    internal ValueTask VerifyAsync(ProtocolStorageScope scope, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var database = scope.Owner.Database;
        var savedJob = await database.Set<ApplicationGrantJob>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == jobId, current);
        var savedApproval = await database.Set<ApplicationGrantApproval>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == approvalId, current);
        var savedAudit = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == auditId, current);
        var savedDetail = await database.Set<ApplicationGrantAudit>().AsNoTracking().SingleOrDefaultAsync(row => row.AuditId == auditId, current);
        if (JsonSerializer.Serialize(localJob) != job || JsonSerializer.Serialize(localApproval) != approval ||
            JsonSerializer.Serialize(localAudit) != audit || JsonSerializer.Serialize(localDetail) != detail ||
            JsonSerializer.Serialize(savedJob) != job || JsonSerializer.Serialize(savedApproval) != approval ||
            JsonSerializer.Serialize(savedAudit) != audit || JsonSerializer.Serialize(savedDetail) != detail)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }, token);
}
