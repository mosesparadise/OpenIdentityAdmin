using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Exercises saved-state substitution and late wall-clock rollback, preserving the unchanged original native entries and counters.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantSaveFailureTests(BootstrapFixture fixture)
{
    private const string Valid = "valid";
    private const string OutputBypass = "grant-frozen-output-bypass";

    /// <summary>Native/audit callbacks cannot redefine the approved baseline or target; all attempted native changes roll back.</summary>
    [Theory]
    [InlineData("PostgreSql", GrantSaveDefect.Counter, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", GrantSaveDefect.Revision, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", GrantSaveDefect.Approval, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", GrantSaveDefect.Credential, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", GrantSaveDefect.Clock, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", GrantSaveDefect.Counter, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", GrantSaveDefect.Revision, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", GrantSaveDefect.Approval, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", GrantSaveDefect.Credential, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", GrantSaveDefect.Clock, Traits = ["Provider", "SqlServer"])]
    public async Task Given_AcceptedJob_When_SaveChangesBaselineTargetOrClock_Then_AttemptRollsBackAndJobStops(string provider, GrantSaveDefect defect)
    {
        var fault = new GrantSaveFault(defect);
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, fault);
        await test.SeedAsync(test.Id, 1, 1);
        var accepted = await test.AdmitAsync(test.Intent());
        var baseline = await test.ReadAsync(accepted.Id);
        var priorHistory = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        fault.Clock = test.Fixture.Clock;
        fault.Armed = true;
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(fault.Observed);
        var stopped = await test.ReadAsync(accepted.Id);
        Assert.Equal(GrantJobState.Stopped, stopped.State);
        Assert.Equal(0, stopped.TokenCount);
        Assert.Equal(0, stopped.AuthorizationCount);
        Assert.Equal(baseline.Revision + 1, stopped.Revision);
        Assert.Equal(baseline.EffectiveApprovalId, stopped.EffectiveApprovalId);
        var history = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        history.Require();
        history.Preserves(priorHistory);
        Assert.Equal(2, history.Details.Length);
        var native = await test.StatusesAsync(test.Id);
        Assert.Equal(Valid, Assert.Single(native.Authorizations));
        Assert.Equal(Valid, Assert.Single(native.Tokens));
        await using var scope = test.Fixture.Scope();
        var policy = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationPolicy>().AsNoTracking().SingleAsync(test.Token);
        Assert.False(policy.Enabled);
        Assert.Equal(test.Application.Application!.Credential!.ExpiresAt.ToUnixTimeSeconds(), policy.CredentialExpiresAt);
    }

    /// <summary>An acceptance-save callback cannot enable the target alongside the new job or leave a false accepted approval behind.</summary>
    [Theory]
    [InlineData("PostgreSql", GrantSaveDefect.Enabled, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", GrantSaveDefect.MemoryOutput, Traits = ["Provider", "PostgreSql", "CriticalDecision", "grant-frozen-output"])]
    [InlineData("SqlServer", GrantSaveDefect.Enabled, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", GrantSaveDefect.MemoryOutput, Traits = ["Provider", "SqlServer", "CriticalDecision", "grant-frozen-output"])]
    public async Task Given_DisabledTarget_When_AdmissionSaveChangesTargetOrOutput_Then_NoJobOrApprovalCommits(string provider, GrantSaveDefect defect)
    {
        var fault = new GrantSaveFault(defect);
        await using var test = await GrantTestContext.CreateAsync(fixture, provider, fault);
        fault.Armed = true;
        var failure = await Record.ExceptionAsync(() => test.AdmitAsync(test.Intent()));
        Assert.True(failure is not null, OutputBypass);
        Assert.True(fault.Observed);
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await database.Set<ApplicationGrantJob>().ToArrayAsync(test.Token));
        Assert.Empty(await database.Set<ApplicationGrantApproval>().ToArrayAsync(test.Token));
        Assert.False((await database.Set<ApplicationPolicy>().SingleAsync(test.Token)).Enabled);
    }
}
