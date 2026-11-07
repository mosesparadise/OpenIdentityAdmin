using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Exercises real relational progress, provenance, concurrency and state constraints for each supported provider.</summary>
public sealed class GrantSchemaTests
{
    /// <summary>Retains maximum-length identities and links all provenance while rejecting an actual stale revision update.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GrantSchema_When_MaximumIdentitiesAndConcurrentUpdatesPersist_Then_ProvenanceAndRevisionAreEnforced(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        await using var first = fixture.Scope();
        var database = first.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = GrantSchemaRows.Add(database);
        await database.SaveChangesAsync(fixture.Token);
        await using var second = fixture.Scope();
        var other = second.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stale = await other.Set<ApplicationGrantJob>().SingleAsync(fixture.Token);
        rows.Job.State = GrantJobState.Processing;
        rows.Job.Revision++;
        await database.SaveChangesAsync(fixture.Token);
        stale.Revision++;
        stale.State = GrantJobState.Succeeded;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync(fixture.Token));
        database.ChangeTracker.Clear();
        Assert.Equal(GrantJobState.Processing, (await database.Set<ApplicationGrantJob>().SingleAsync(fixture.Token)).State);
        Assert.Equal(450, (await database.Set<ApplicationGrantApproval>().SingleAsync(fixture.Token)).ActorId.Length);
        Assert.Equal(400, (await database.Set<ApplicationPolicy>().SingleAsync(fixture.Token)).ApplicationId.Length);
        Assert.Equal(rows.Approval.Id, (await database.Set<ApplicationGrantAudit>().SingleAsync(fixture.Token)).ApprovalId);
    }

    /// <summary>Requires actual database rejection and rollback for invalid progress, state, time and audit relationships.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GrantSchema_When_InvalidStateOrProvenanceIsSaved_Then_TheWholeInsertIsRejected(string provider)
    {
        await using var fixture = await ManagedSchemaFixture.CreateAsync(provider);
        foreach (var defect in Enum.GetValues<Defect>())
        {
            await using var scope = fixture.Scope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var rows = GrantSchemaRows.Add(database);
            Corrupt(rows.Job, rows.Approval, rows.Detail, defect);
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(fixture.Token));
            await using var fresh = fixture.Scope();
            Assert.Empty(await fresh.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationGrantJob>().ToArrayAsync(fixture.Token));
        }
    }

    /// <summary>Changes one independent relational invariant at a time; valid remaining fields retain the control insert shape.</summary>
    private static void Corrupt(ApplicationGrantJob job, ApplicationGrantApproval approval, ApplicationGrantAudit detail, Defect defect)
    {
        switch (defect)
        {
            case Defect.Count:
                job.AuthorizationCount = -1;
                break;
            case Defect.Revision:
                job.Revision = 0;
                break;
            case Defect.State:
                job.State = (GrantJobState)0;
                break;
            case Defect.StoppedReason:
                job.State = GrantJobState.Stopped;
                break;
            case Defect.Time:
                job.ObservedAt = job.CreatedAt - 1;
                break;
            case Defect.ApprovalTime:
                approval.ReplayUntil = approval.ApprovedAt;
                break;
            case Defect.Action:
                approval.Action = (GrantApprovalAction)0;
                break;
            case Defect.AuditOwner:
                detail.ApprovalId = Guid.NewGuid();
                break;
            case Defect.Delta:
                detail.TokenDelta = 1;
                break;
        }
    }

    private enum Defect
    {
        Count, Revision, State, StoppedReason, Time, ApprovalTime, Action, AuditOwner, Delta
    }
}
