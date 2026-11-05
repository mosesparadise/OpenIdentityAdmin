using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Distinguishes an actual rollback from lost commit acknowledgement through independent native, job, approval and audit observations.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantCommitTests(BootstrapFixture fixture)
{
    private const string Valid = "valid";
    private const string Revoked = "revoked";
    private const string GuidFormat = "D";

    /// <summary>Committed batches reconcile once without an invented stopped revision; failed commits preserve native states and record a zero-progress stop.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_AcceptedJob_When_CommitBoundaryFails_Then_OnlyActualCommittedProgressIsReconciled(string provider, bool after)
    {
        var fault = new GrantCommitFault(after);
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, fault);
        await test.SeedAsync(test.Id, 1, 1);
        var accepted = await test.AdmitAsync(test.Intent());
        var baseline = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        fault.Armed = true;
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(fault.Observed);
        var history = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        history.Require();
        history.Preserves(baseline);
        Assert.Equal(2, history.Job.Revision);
        Assert.Equal(after ? GrantJobState.Processing : GrantJobState.Stopped, history.Job.State);
        Assert.Equal(after ? 1 : 0, history.Job.TokenCount);
        Assert.Equal(after ? 1 : 0, history.Job.AuthorizationCount);
        var native = await test.StatusesAsync(test.Id);
        Assert.Equal(after ? Revoked : Valid, Assert.Single(native.Tokens));
        Assert.Equal(after ? Revoked : Valid, Assert.Single(native.Authorizations));
        Assert.Equal(after, await test.BatchAsync(accepted.Id));
        var final = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        final.Require();
        final.Preserves(history);
        Assert.Equal(after ? GrantJobState.Succeeded : GrantJobState.Stopped, final.Job.State);
        Assert.Equal(history.Job.TokenCount, final.Job.TokenCount);
    }

    /// <summary>A lost acceptance acknowledgement leaves exactly one durable approval and is recovered by the same currently authorized key and intent.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_AdmissionCommit_When_AcknowledgementIsLost_Then_SameKeyRecoversExactlyOneDurableApproval(string provider)
    {
        var fault = new GrantCommitFault(after: true);
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, fault);
        var intent = test.Intent();
        fault.Armed = true;
        await Assert.ThrowsAnyAsync<Exception>(() => test.AdmitAsync(intent));
        Assert.True(fault.Observed);
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var original = await database.Set<ApplicationGrantJob>().AsNoTracking().SingleAsync(test.Token);
        var history = await GrantAuditWitness.ReadAsync(test, original.Id.ToString(GuidFormat));
        var replay = await test.AdmitAsync(intent);
        Assert.Equal(original.Id.ToString(GuidFormat), replay.Id);
        var recovered = await GrantAuditWitness.ReadAsync(test, replay.Id);
        recovered.Require();
        recovered.Preserves(history);
        Assert.Single(recovered.Approvals);
        Assert.Single(recovered.Audits);
        Assert.Equal(intent.RequestKey, recovered.Approvals[0].RequestKey);
    }
}
