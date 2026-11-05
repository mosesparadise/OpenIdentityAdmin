using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves actual lifecycle SQL/audit failures roll back every field and retain the original usable version on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationLifecycleFailureTests(BootstrapFixture fixture)
{
    private const string Maintenance = "maintenance";

    /// <summary>Injects failure only after persisted SQL, or denies the real audit INSERT, then verifies atomic rollback and deliberate recovery.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_LifecycleWrite_When_PostSaveOrAuditInsertFails_Then_StateVersionReasonAndAuditRollBackTogether(string provider, bool enabled, bool denyAudit)
    {
        var fault = new ApplicationUpdateFault(!denyAudit);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        var current = await ApplicationLifecycleTests.PrepareAsync(test, enabled);
        fault.ApplicationId = current.Application!.Id;
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, fault.ApplicationId);
        var command = ApplicationMutationCommand.Lifecycle(fault.ApplicationId, current.Version!, new(Maintenance, true), enabled)!;
        fault.Armed = true;
        await ApplicationUpdateTests.FailAsync(test, command, denyAudit, fault);
        if (!denyAudit)
        {
            Assert.NotNull(fault.Persisted);
            var audit = Assert.Single(fault.Persisted.Audits, row => !before.Audits.Any(prior => prior.Id == row.Id));
            ApplicationLifecycleOracle.Require(test, before, fault.Persisted, enabled, audit.CorrelationId);
        }
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, fault.ApplicationId)).Digest);
        fault.Armed = false;
        var correlation = Guid.NewGuid();
        var recovery = await test.ExecuteOnAsync(test.Api, command, correlation, test.Token);
        var after = await ApplicationUpdateSnapshot.ReadAsync(test, fault.ApplicationId);
        ApplicationLifecycleOracle.Require(test, before, after, enabled, correlation);
        ApplicationLifecycleOracle.Output(recovery, after);
    }

    /// <summary>Rejects a valid-but-wrong reason written after actual save, proving verification reads persisted rows before commit.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql", "CriticalDecision", "application-lifecycle-reason"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer", "CriticalDecision", "application-lifecycle-reason"])]
    public async Task Given_PersistedLifecycleReasonChanged_When_VerifiedBeforeCommit_Then_ExactStateRollsBack(string provider)
    {
        const string bypass = "application-lifecycle-reason-bypass";
        var fault = new ApplicationWriteFault(ApplicationWriteDefect.ReasonChanged);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        var current = await ApplicationLifecycleTests.PrepareAsync(test, enabled: false);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, current.Application!.Id);
        var command = ApplicationMutationCommand.Lifecycle(current.Application.Id, current.Version!, new(Maintenance, true), false)!;
        fault.Armed = true;
        var failure = await Record.ExceptionAsync(() => test.ExecuteAsync(command));
        Assert.True(fault.Fired);
        Assert.True(failure is not null, bypass);
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, current.Application.Id)).Digest);
    }

}
