using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Changes real persisted permission or current MFA time after admission staging, before the coordinator's final authority observation.</summary>
internal sealed class GrantAdmissionChange(bool expireMfa) : SaveChangesInterceptor
{
    internal bool Armed
    {
        get; set;
    }
    internal bool Observed
    {
        get; private set;
    }
    internal FactorClock Clock { get; set; } = null!;

    /// <summary>Consumes one actual grant-save callback; permission removal participates in the transaction and must roll back with the rejected admission.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (!Armed || eventData.Context is not PlatformDbContext database || !database.ChangeTracker.Entries<ApplicationGrantApproval>().Any())
        {
            return result;
        }
        Armed = false;
        Observed = true;
        if (expireMfa)
        {
            Clock.Now = Clock.Now.AddSeconds(1);
        }
        else
        {
            Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == GrantBoundaryEvidence.Permission).ExecuteDeleteAsync(cancellationToken) > 0);
        }
        return result;
    }
}
