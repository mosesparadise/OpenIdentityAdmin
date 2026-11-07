using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Observes complete grant and native state independently of response projections, preserving audit and target isolation evidence.</summary>
internal static class GrantBoundaryEvidence
{
    internal const string Body = "{\"reason\":\"security\",\"confirm\":true}";
    internal const string Permission = "grants.revoke";
    internal const string GuidFormat = "D";
    internal const string Suffix = "/grant-revocations";
    internal const string Separator = "/";

    /// <summary>Hashes every grant row and audit together with complete native associations and the target's configuration.</summary>
    internal static async Task<string> SnapshotAsync(PlatformDbContext database, string id)
    {
        var token = TestContext.Current.CancellationToken;
        var application = await ApplicationUpdateSnapshot.ReadAsync(database, id, token);
        var native = await ManagedJourneyEvidence.NativeAsync(database);
        return ApplicationUpdateSnapshot.Hash(new
        {
            application.Digest,
            native,
            Jobs = await database.Set<ApplicationGrantJob>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Approvals = await database.Set<ApplicationGrantApproval>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Details = await database.Set<ApplicationGrantAudit>().AsNoTracking().OrderBy(row => row.AuditId).ToArrayAsync(token),
            Audits = await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)
        });
    }
}
