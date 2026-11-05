using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Checks original authority after actual waits and distinguishes cancelled writes from ambiguous successful commits.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationLifecycleBoundaryTests(BootstrapFixture fixture)
{
    private const string Maintenance = "maintenance";
    private const string Permission = "applications.disable";
    private const int LastMfaSecond = 299;
    private const int OperationSeconds = 5;

    /// <summary>Current permission removal and exact MFA expiry while blocked cannot be replaced by an earlier admission observation.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_LifecycleWaitingOnGovernance_When_CurrentGrantOrMfaEnds_Then_NoStateReasonOrAuditCommits(string provider, bool enabled, bool expireMfa)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var current = await ApplicationLifecycleTests.PrepareAsync(test, enabled);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, current.Application!.Id);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        if (expireMfa)
        {
            var verified = await database.Set<AdministratorSession>().Select(row => row.FactorVerifiedAt).SingleAsync(test.Token);
            test.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(verified + LastMfaSecond);
        }
        var lease = test.Scenario.Account.Journey.Lease;
        await using var held = await GovernanceLock.AcquireAsync(lease);
        var pending = test.ExecuteAsync(ApplicationMutationCommand.Lifecycle(current.Application.Id, current.Version!, new(Maintenance, true), enabled)!);
        var outcomes = await ApplicationUpdateTests.ReleaseAndJoinAsync(held, async () =>
        {
            await held.ObserveWaitersAsync(lease, 1);
            if (expireMfa)
            {
                test.Clock.Now = test.Clock.Now.AddSeconds(1);
            }
            else
            {
                Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == Permission).ExecuteDeleteAsync(test.Token) > 0);
            }
        }, pending);
        Assert.Equal(403, Assert.Single(outcomes).Status);
        Assert.Null(outcomes[0].Application);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, current.Application.Id)).Digest);
    }

    /// <summary>Cancellation immediately after actual lifecycle/audit save rolls back all rows and never returns a successful result.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PersistedLifecycleWrite_When_CallerCancels_Then_ExactStateRollsBackWithoutPublication(string provider, bool enabled)
    {
        using var cancellation = new CancellationTokenSource();
        var fault = new ApplicationWriteFault(ApplicationWriteDefect.Cancelled) { Advance = cancellation.Cancel };
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        var current = await ApplicationLifecycleTests.PrepareAsync(test, enabled);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, current.Application!.Id);
        var command = ApplicationMutationCommand.Lifecycle(current.Application.Id, current.Version!, new(Maintenance, true), enabled)!;
        fault.Armed = true;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.ExecuteWithCancellationAsync(command, cancellation.Token));
        Assert.True(fault.Fired);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, current.Application.Id)).Digest);
    }

    /// <summary>Actual provider commit remains durable after acknowledgement or disposal failure, but the caller never receives success or an automatic retry.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CommittedLifecycleWrite_When_AcknowledgementOrCleanupFails_Then_OneDurableMutationRemainsWithoutSuccessOutput(string provider, bool enabled, bool acknowledgement)
    {
        var fault = new ApplicationMutationCleanupFault(acknowledgement);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, cleanup: fault);
        var current = await ApplicationLifecycleTests.PrepareAsync(test, enabled);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, current.Application!.Id);
        var command = ApplicationMutationCommand.Lifecycle(current.Application.Id, current.Version!, new(Maintenance, true), enabled)!;
        var correlation = Guid.NewGuid();
        fault.Armed = true;
        var failure = await Record.ExceptionAsync(() => test.ExecuteOnAsync(test.Api, command, correlation, test.Token));
        Assert.True(fault.Contains(failure));
        Assert.Equal(1, fault.Commits);
        Assert.Equal(1, fault.Disposals);
        ApplicationLifecycleOracle.Require(test, before, await ApplicationUpdateSnapshot.ReadAsync(test, current.Application.Id), enabled, correlation);
    }

    /// <summary>A clock rollback or equality at the original operation deadline after real save cannot commit lifecycle authority.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_LifecycleSavedBeforeCommit_When_ClockRollsBackOrBudgetExpires_Then_OriginalBoundsPreventCommit(string provider, bool rollback)
    {
        var fault = new ApplicationWriteFault(ApplicationWriteDefect.Cancelled);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        var current = await ApplicationLifecycleTests.PrepareAsync(test, enabled: false);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, current.Application!.Id);
        var command = ApplicationMutationCommand.Lifecycle(current.Application.Id, current.Version!, new(Maintenance, true), false)!;
        fault.Advance = () => test.Clock.Now = test.Clock.Now.AddSeconds(rollback ? -1 : OperationSeconds);
        fault.Armed = true;
        if (rollback)
        {
            var denied = await test.ExecuteAsync(command);
            Assert.Equal(401, denied.Status);
            Assert.Null(denied.Application);
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.ExecuteAsync(command));
        }
        Assert.True(fault.Fired);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, current.Application.Id)).Digest);
    }
}
