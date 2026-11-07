using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Rechecks current security bindings and fixed catalogue permission against the immutable original deletion approval.</summary>
internal static class CatalogueDeletionActor
{
    private const string ScopePermission = "scopes.write";
    private const string AudiencePermission = "audiences.write";

    /// <summary>Does not require a still-open browser session or obtain new MFA; exact recorded generations and original deadline remain mandatory.</summary>
    internal static ValueTask<bool> AllowsAsync(ProtocolStorageScope scope, ProtocolAdmissionOwnership ownership,
        CatalogueDeletionSnapshot job, CancellationToken token)
    {
        var database = (PlatformDbContext)scope.Owner.Database;
        return ownership.RunAsync(database, async current =>
        {
            if (ownership.UserId != job.ActorId || !CurrentTime(scope.Owner.Clock, job))
            {
                return false;
            }
            var account = await AdministratorCurrentAccount.ReadAsync(database, job.ActorId, scope.Owner.Clock, current);
            if (account is null || account.Stamp != job.Stamp || account.Generation != job.Generation ||
                account.FactorGeneration != job.FactorGeneration || account.EnrollmentRevision != job.EnrollmentRevision)
            {
                return false;
            }
            var permission = job.Kind switch
            {
                CatalogueVersion.Scope => ScopePermission,
                CatalogueVersion.Audience => AudiencePermission,
                _ => throw ProtocolStorageOwner.Unavailable()
            };
            var grants = await new GovernanceGrants(database).ReadProtocolAsync(ownership, current);
            return grants.Permissions(job.ActorId).Contains(permission) && CurrentTime(scope.Owner.Clock, job);
        }, token);
    }

    /// <summary>Rejects clock rollback and equality at the earliest original source/native/MFA deadline without renewing approval.</summary>
    internal static bool CurrentTime(TimeProvider clock, CatalogueDeletionSnapshot job)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        return now >= job.ObservedAt && now >= job.CreatedAt && now < job.ApprovalUntil;
    }
}
