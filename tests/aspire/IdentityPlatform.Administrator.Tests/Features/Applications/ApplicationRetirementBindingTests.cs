using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Validates independent review findings with real persisted approval substitutions rather than fabricated exceptions or response-only checks.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationRetirementBindingTests(BootstrapFixture fixture)
{
    private const string Reason = "security";
    private const string Permission = "grants.revoke";
    private const string GuidFormat = "D";
    private const int Conflict = 409;
    private const string EffectiveMarker = "retirement-effective-approval-bypass";

    /// <summary>Relabeling a genuine later Continue as Submit cannot authorize a worker batch or an original retirement replay.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    [Trait("CriticalDecision", "retirement-effective-approval")]
    public async Task Given_ContinuedRetirement_When_EffectiveApprovalIsRelabeledSubmit_Then_NativeGrantsRemainUnchangedAndReplayFailsClosed(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        await test.Grants.SeedAsync(test.Id, 1, 1);
        var intent = test.Intent();
        var accepted = await test.RetireAsync(intent);
        var stopped = await StopAsync(test, accepted.Job.Id);
        await test.Grants.AdmitAsync(Continue(test, accepted.ApplicationVersion, stopped));
        var active = await test.Grants.ReadAsync(accepted.Job.Id);
        var original = await test.ReceiptAsync();
        Assert.NotEqual(original.GrantApprovalId, active.EffectiveApprovalId);
        var native = await test.Grants.NativeSnapshotAsync();
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Set<ApplicationGrantApproval>().Where(row => row.Id == active.EffectiveApprovalId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Action, GrantApprovalAction.Submit), test.Token));
        await RequireCorruptedReplayAsync(test, intent, active, native, original);
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        var failed = await test.Grants.ReadAsync(accepted.Job.Id);
        Assert.Equal(GrantJobState.Stopped, failed.State);
        Assert.Equal(GrantJobFailure.Storage, failed.Failure);
        Assert.Equal(native, await test.Grants.NativeSnapshotAsync());
        Assert.Equal(ApplicationUpdateSnapshot.Hash(original), ApplicationUpdateSnapshot.Hash(await test.ReceiptAsync()));
        Assert.Equal(1, await database.Set<ApplicationGrantApproval>().Where(row => row.Id == active.EffectiveApprovalId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Action, GrantApprovalAction.Continue), test.Token));
        await test.Grants.AdmitAsync(Continue(test, accepted.ApplicationVersion, failed));
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        Assert.Equal(GrantJobState.Succeeded, (await test.Grants.ReadAsync(accepted.Job.Id)).State);
    }

    /// <summary>An old non-effective Continue receipt cannot redirect exact replay to a different completed job even when that job belongs to the same application.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_LaterEffectiveContinuation_When_OlderApprovalJobIsSubstituted_Then_ExactReplayCannotReturnUnrelatedPriorWork(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        var disabled = await test.Fixture.ExecuteAsync(ApplicationMutationCommand.Lifecycle(test.Id, test.Application.Version!, new(Reason, true), false)!);
        Assert.Equal(200, disabled.Status);
        var prior = await test.Grants.AdmitAsync(GrantIntent.Create(test.Id, disabled.Version!, Guid.NewGuid().ToString(GuidFormat), new(Reason, true))!);
        Assert.True(await test.Grants.BatchAsync(prior.Id));
        Assert.Equal(GrantJobState.Succeeded, (await test.Grants.ReadAsync(prior.Id)).State);
        var accepted = await test.RetireAsync(test.Intent() with
        {
            Version = disabled.Version!
        });
        var firstIntent = Continue(test, accepted.ApplicationVersion, await StopAsync(test, accepted.Job.Id));
        await test.Grants.AdmitAsync(firstIntent);
        var first = await test.Grants.ReadAsync(accepted.Job.Id);
        await test.Grants.AdmitAsync(Continue(test, accepted.ApplicationVersion, await StopAsync(test, accepted.Job.Id)));
        var second = await test.Grants.ReadAsync(accepted.Job.Id);
        Assert.NotEqual(first.EffectiveApprovalId, second.EffectiveApprovalId);
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Set<ApplicationGrantApproval>().Where(row => row.Id == first.EffectiveApprovalId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.JobId, Guid.Parse(prior.Id)), test.Token));
        var before = await test.SnapshotAsync();
        Assert.Equal(Conflict, (await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.Grants.AdmitAsync(firstIntent))).Status);
        Assert.Equal(before, await test.SnapshotAsync());
        Assert.Equal(1, await database.Set<ApplicationGrantApproval>().Where(row => row.Id == first.EffectiveApprovalId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.JobId, first.Id), test.Token));
        var recovered = await test.Grants.AdmitAsync(firstIntent);
        Assert.Equal(accepted.Job.Id, recovered.Id);
        Assert.Equal(ApplicationVersion.Create(accepted.Job.Id, second.Revision), recovered.Version);
    }

    /// <summary>Marks only a successful wrong-origin replay followed by real native revocation under the same substituted effective approval.</summary>
    private static async Task RequireCorruptedReplayAsync(ApplicationRetirementTestContext test, ApplicationRetirementIntent intent,
        ApplicationGrantJob active, string native, ApplicationRetirement original)
    {
        IdentityPlatform.Core.Features.Applications.ApplicationRetirementAcceptance? replay = null;
        var failure = await Record.ExceptionAsync(async () => replay = await test.RetireAsync(intent));
        if (failure is null)
        {
            Assert.NotNull(replay);
            Assert.Equal(active.Id.ToString(GuidFormat), replay.Job.Id);
            Assert.True(await test.Grants.BatchAsync(replay.Job.Id));
            var advanced = await test.Grants.ReadAsync(replay.Job.Id);
            Assert.Equal(active.EffectiveApprovalId, advanced.EffectiveApprovalId);
            Assert.True(advanced.Revision > active.Revision);
            Assert.True(advanced.AuthorizationCount > 0 && advanced.TokenCount > 0);
            Assert.NotEqual(native, await test.Grants.NativeSnapshotAsync());
            Assert.Equal(ApplicationUpdateSnapshot.Hash(original), ApplicationUpdateSnapshot.Hash(await test.ReceiptAsync()));
            Assert.Fail(EffectiveMarker);
        }
        Assert.IsType<InvalidOperationException>(failure);
    }

    /// <summary>Uses actual permission loss and the real worker to stop the exact current job, then restores only the previous grant claims.</summary>
    private static async Task<ApplicationGrantJob> StopAsync(ApplicationRetirementTestContext test, string id)
    {
        var claims = await ApplicationRetirementAuthorityTests.RemoveAsync(test, Permission);
        Assert.True(await test.Grants.BatchAsync(id));
        var stopped = await test.Grants.ReadAsync(id);
        Assert.Equal(GrantJobState.Stopped, stopped.State);
        Assert.Equal(GrantJobFailure.Authority, stopped.Failure);
        await ApplicationRetirementAuthorityTests.RestoreAsync(test, claims);
        return stopped;
    }

    /// <summary>Constructs a new explicit Continue bound to current application and stopped job versions.</summary>
    private static GrantIntent Continue(ApplicationRetirementTestContext test, string version, ApplicationGrantJob job) =>
        GrantIntent.Create(test.Id, version, Guid.NewGuid().ToString(GuidFormat), new(Reason, true, ApplicationVersion.Create(job.Id.ToString(GuidFormat), job.Revision)), job.Id)!;
}
