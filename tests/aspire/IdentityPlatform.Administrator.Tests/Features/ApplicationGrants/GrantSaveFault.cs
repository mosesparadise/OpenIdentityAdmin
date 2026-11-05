using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Mutates actual persisted state during a native or audit save to exercise frozen expectations and outer rollback.</summary>
internal sealed class GrantSaveFault(GrantSaveDefect defect) : SaveChangesInterceptor
{
    private const int CorruptedProgress = 100;
    private const int ClockRollbackSeconds = -1;
    private const string Revoked = "revoked";

    internal bool Armed
    {
        get; set;
    }
    internal bool Observed
    {
        get; private set;
    }
    internal FactorClock? Clock
    {
        get; set;
    }

    /// <summary>Runs once after the relevant real save; recursive persistence remains in the same outer transaction.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (!Armed || eventData.Context is not PlatformDbContext database || !Selected(database))
        {
            return result;
        }
        Armed = false;
        Observed = true;
        if (defect == GrantSaveDefect.MemoryOutput)
        {
            database.ChangeTracker.Entries<ApplicationGrantJob>().Single().Entity.State = IdentityPlatform.Core.Features.ApplicationGrants.GrantJobState.Succeeded;
            return result;
        }
        if (defect == GrantSaveDefect.Clock)
        {
            Clock!.Now = Clock.Now.AddSeconds(ClockRollbackSeconds);
            return result;
        }
        if (defect is GrantSaveDefect.Counter or GrantSaveDefect.Revision or GrantSaveDefect.Approval)
        {
            var job = await database.Set<ApplicationGrantJob>().SingleAsync(cancellationToken);
            if (defect == GrantSaveDefect.Counter)
            {
                job.TokenCount = CorruptedProgress;
            }
            if (defect == GrantSaveDefect.Revision)
            {
                job.Revision += CorruptedProgress;
            }
            if (defect == GrantSaveDefect.Approval)
            {
                job.EffectiveApprovalId = Guid.NewGuid();
            }
        }
        else
        {
            var policy = await database.Set<ApplicationPolicy>().SingleAsync(cancellationToken);
            if (defect == GrantSaveDefect.Enabled)
            {
                policy.Enabled = true;
            }
            else
            {
                policy.CredentialExpiresAt++;
            }
        }
        await database.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <summary>Targets actual changed native tokens, or the admission/progress audit save for the two late-boundary defects.</summary>
    private bool Selected(PlatformDbContext database) => defect switch
    {
        GrantSaveDefect.Enabled or GrantSaveDefect.MemoryOutput or GrantSaveDefect.Clock => database.ChangeTracker.Entries<ApplicationGrantAudit>().Any(),
        _ => database.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>().Any(entry => entry.Entity.Status == Revoked)
    };
}

/// <summary>Names actual save-callback mutations exercised independently on both relational providers.</summary>
public enum GrantSaveDefect
{
    Counter, Revision, Approval, Credential, Enabled, Clock, MemoryOutput
}
