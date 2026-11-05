using System.Text.Json;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Independently reconciles committed revision history, approval provenance and security audit cardinality from a fresh context.</summary>
internal sealed record GrantAuditWitness(ApplicationGrantJob Job, ApplicationGrantApproval[] Approvals, ApplicationGrantAudit[] Details, SecurityAuditIntent[] Audits)
{
    private const string Prefix = "ApplicationGrantRevocation";
    private const string Accepted = Prefix + "Accepted";
    private const string Continued = Prefix + "Continued";
    private const string Advanced = Prefix + "Advanced";
    private const string Stopped = Prefix + "Stopped";
    private const string Succeeded = Prefix + "Succeeded";
    private const string User = "User";
    private const string Application = "Application";
    private const string Outcome = "Accepted";

    /// <summary>Reads all history only for the small isolated test job; no production query gains an unbounded inventory.</summary>
    internal static async Task<GrantAuditWitness> ReadAsync(GrantTestContext test, string id)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await ReadAsync(database, Guid.Parse(id), test.Token);
    }

    /// <summary>Shares the independent audit oracle with actual browser-issued jobs observed through a fresh ordinary fixture context.</summary>
    internal static async Task<GrantAuditWitness> ReadAsync(PlatformDbContext database, Guid jobId, CancellationToken token)
    {
        var job = await database.Set<ApplicationGrantJob>().AsNoTracking().SingleAsync(row => row.Id == jobId, token);
        return new(job,
            await database.Set<ApplicationGrantApproval>().AsNoTracking().Where(row => row.JobId == jobId).OrderBy(row => row.Id).ToArrayAsync(token),
            await database.Set<ApplicationGrantAudit>().AsNoTracking().Where(row => row.JobId == jobId).OrderBy(row => row.JobRevision).ToArrayAsync(token),
            await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => row.TargetId == job.ApplicationId && row.Event.StartsWith(Prefix)).ToArrayAsync(token));
    }

    /// <summary>Requires one security intent per committed revision, exact deltas and effective approval, including zero-delta acceptance and stopped outcomes.</summary>
    internal void Require()
    {
        Assert.Equal(Job.Revision, Details.LongLength);
        Assert.Equal(Details.Length, Audits.Length);
        long authorizations = 0, tokens = 0;
        for (var index = 0; index < Details.Length; index++)
        {
            var detail = Details[index];
            Assert.Equal(index + 1, detail.JobRevision);
            var audit = Assert.Single(Audits, row => row.Id == detail.AuditId);
            var approval = Assert.Single(Approvals, row => row.Id == detail.ApprovalId);
            Provenance(detail, audit, approval);
            authorizations += detail.AuthorizationDelta;
            tokens += detail.TokenDelta;
            Assert.Equal(authorizations, detail.AuthorizationCount);
            Assert.Equal(tokens, detail.TokenCount);
            if (detail.State != GrantJobState.Processing)
            {
                Assert.Equal(0, detail.AuthorizationDelta);
                Assert.Equal(0, detail.TokenDelta);
            }
        }
        Assert.Equal(Job.AuthorizationCount, authorizations);
        Assert.Equal(Job.TokenCount, tokens);
        Assert.Equal(Job.State, Details[^1].State);
        Assert.Equal(Job.Failure, Details[^1].Failure);
        Assert.Equal(Job.EffectiveApprovalId, Details[^1].ApprovalId);
    }

    /// <summary>Checks historical approvals and receipts are unchanged after any later batch, stop or continuation.</summary>
    internal void Preserves(GrantAuditWitness previous)
    {
        foreach (var approval in previous.Approvals)
        {
            Assert.Equal(JsonSerializer.Serialize(approval), JsonSerializer.Serialize(Assert.Single(Approvals, row => row.Id == approval.Id)));
        }
        foreach (var detail in previous.Details)
        {
            Assert.Equal(JsonSerializer.Serialize(detail), JsonSerializer.Serialize(Assert.Single(Details, row => row.AuditId == detail.AuditId)));
            var before = Assert.Single(previous.Audits, row => row.Id == detail.AuditId);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(Assert.Single(Audits, row => row.Id == before.Id)));
        }
    }

    /// <summary>Verifies independent wire event semantics and immutable actor/target approval association.</summary>
    private void Provenance(ApplicationGrantAudit detail, SecurityAuditIntent audit, ApplicationGrantApproval approval)
    {
        Assert.Equal(Job.Id, detail.JobId);
        Assert.Equal(Job.Id, approval.JobId);
        Assert.Equal(Job.ApplicationId, approval.ApplicationId);
        Assert.Equal(approval.ActorId, audit.ActorId);
        Assert.Equal(User, audit.ActorType);
        Assert.Equal(Application, audit.TargetType);
        Assert.Equal(Job.ApplicationId, audit.TargetId);
        Assert.Equal(Outcome, audit.Outcome);
        Assert.NotEqual(Guid.Empty, audit.CorrelationId);
        Assert.True(audit.OccurredAt >= approval.ApprovedAt);
        var action = detail.State switch
        {
            GrantJobState.Queued => approval.Action == GrantApprovalAction.Submit ? Accepted : Continued,
            GrantJobState.Processing => Advanced,
            GrantJobState.Stopped => Stopped,
            GrantJobState.Succeeded => Succeeded,
            _ => throw new InvalidOperationException()
        };
        Assert.Equal(action, audit.Event);
        if (detail.State == GrantJobState.Queued)
        {
            Assert.Equal(approval.AuditId, audit.Id);
        }
    }
}
