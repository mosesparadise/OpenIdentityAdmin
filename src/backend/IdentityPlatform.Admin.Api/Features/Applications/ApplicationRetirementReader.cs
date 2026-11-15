using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Projects only the bounded terminal recovery reference under existing application-read authority.</summary>
internal static class ApplicationRetirementReader
{
    private const string GuidFormat = "D";

    /// <summary>Requires exact terminal policy and original job/approval associations without exposing approver or idempotency material.</summary>
    internal static async Task<ApplicationRetirementReference?> ReadAsync(PlatformDbContext database, ApplicationPolicy policy, CancellationToken token)
    {
        if (!policy.Retired)
        {
            return null;
        }
        var receipt = await database.Set<ApplicationRetirement>().AsNoTracking().SingleOrDefaultAsync(row => row.ApplicationId == policy.ApplicationId, token);
        if (receipt is null || receipt.ApplicationId != policy.ApplicationId || policy.Enabled || receipt.RetiredRevision != policy.Revision ||
            receipt.OriginalRevision <= 0 || receipt.RetiredRevision - receipt.OriginalRevision != 1 || receipt.RetiredAt != policy.ObservedAt ||
            receipt.RetiredAt < policy.CredentialIssuedAt || receipt.GrantJobId == Guid.Empty || receipt.GrantApprovalId == Guid.Empty)
        {
            throw new InvalidDataException();
        }
        var job = await database.Set<ApplicationGrantJob>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == receipt.GrantJobId, token);
        var approval = await database.Set<ApplicationGrantApproval>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == receipt.GrantApprovalId, token);
        if (job is null || job.ApplicationId != policy.ApplicationId || job.ApplicationRevision != policy.Revision || job.CreatedAt != receipt.RetiredAt ||
            approval is null || approval.ApplicationId != policy.ApplicationId || approval.JobId != job.Id ||
            approval.ApplicationRevision != policy.Revision || approval.Action != GrantApprovalAction.Submit)
        {
            throw new InvalidDataException();
        }
        return new(receipt.GrantJobId.ToString(GuidFormat), DateTimeOffset.FromUnixTimeSeconds(receipt.RetiredAt));
    }
}
