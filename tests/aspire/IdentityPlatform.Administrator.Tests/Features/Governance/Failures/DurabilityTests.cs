using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Proves real committed governance survives owner loss and is reconciled by a fresh process without reexecution.</summary>
[Collection("administrator-bootstrap")]
public sealed class DurabilityTests(BootstrapFixture fixture)
{
    private const string LostResult = "lost-result-and-restart";
    private const string Revision = "authorityRevision";
    private const string Assigned = "AdministratorBundleAssigned";
    private const int ExpectedActorReplayEntries = 2;
    private const string Accepted = "Accepted";
    public static IEnumerable<TheoryDataRow<string, string>> Cases => GovernanceRows.For(LostResult);

    /// <summary>The child owns and commits the native mutation; a privileged milestone gates its death before the final result frame is delivered.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_CommittedCommand_When_OwnerDiesBeforeReturning_Then_NewNodeReconcilesWithoutReexecution(string provider, string condition)
    {
        Assert.Equal(LostResult, condition);
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.Assign);
        var code = await scenario.UnusedCodeAsync();
        var before = await scenario.SnapshotAsync();
        await using (var child = new GovernanceDriver())
        {
            await GovernanceDriverInput.ExecuteAsync(child.Input, scenario, provider, request, scenario.Session,
                scenario.Password, code, waitForAcknowledgement: true, child.Token);
            using var ready = await child.ReadFrameAsync(GovernanceDriver.Ready);
            using var committed = await child.ReadFrameAsync(GovernanceDriver.Committed);
            await RequireCommittedAsync(scenario, request.UserId!, request.RoleId!);
            await child.KillAsync();
        }
        var committedState = await scenario.SnapshotAsync();
        Assert.NotEqual(before, committedState);
        await using (var restarted = new GovernanceDriver())
        {
            await GovernanceDriverInput.ReadAsync(restarted.Input, scenario, provider, restarted.Token);
            using var ready = await restarted.ReadFrameAsync(GovernanceDriver.Ready);
            using var result = await restarted.ReadFrameAsync(GovernanceDriver.Result);
            Assert.Equal(1, result.RootElement.GetProperty(Revision).GetInt64());
            await restarted.RequireExitAsync(0);
        }
        Assert.Equal(committedState, await scenario.SnapshotAsync());
    }

    /// <summary>Independently verifies actual mutation, one semantic version, one exact audit and permanent bootstrap ownership before owner loss.</summary>
    private static async Task RequireCommittedAsync(GovernanceScenario scenario, string target, string role)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        Assert.True(await database.UserRoles.AnyAsync(row => row.UserId == target && row.RoleId == role, token));
        var state = await database.Set<AdministratorGovernance>().SingleAsync(token);
        Assert.Equal(1, state.AuthorityRevision);
        Assert.True(state.BootstrapConsumed);
        Assert.Equal(scenario.ActorId, await database.Set<AdministratorBootstrapReceipt>().Select(row => row.UserId).SingleAsync(token));
        var audit = Assert.Single(await database.Set<SecurityAuditIntent>().Where(row => row.Event == Assigned).ToArrayAsync(token));
        Assert.Equal(scenario.ActorId, audit.ActorId);
        Assert.Equal(target, audit.TargetId);
        Assert.Equal(Accepted, audit.Outcome);
        Assert.Equal(ExpectedActorReplayEntries, await database.Set<AdministratorReplay>().CountAsync(token));
    }
}
