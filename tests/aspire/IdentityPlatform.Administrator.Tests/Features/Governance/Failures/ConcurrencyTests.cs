using System.Diagnostics;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Exercises actual serialized governance locks and authoritative relationship rereads on both native providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ConcurrencyTests(BootstrapFixture fixture)
{
    private const string DeadlineCase = "five-second-budget";
    private const string CancellationCase = "caller-cancellation";
    public static IEnumerable<TheoryDataRow<string, string>> PlanChanges => GovernanceRows.For(
        GovernancePlanChange.AffectedMember, GovernancePlanChange.EarlierWitness);

    /// <summary>Changed discovered membership invalidates original intent under its original locks, without any late lock expansion.</summary>
    [Theory, MemberData(nameof(PlanChanges))]
    public async Task Given_DiscoveredLockPlan_When_AffectedRelationshipsChange_Then_OperationDeniesWithoutLateLocks(string provider, string variant)
    {
        var trace = new GovernanceLockTrace();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(trace)));
        var change = await GovernancePlanChange.CreateAsync(scenario, variant);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RestoreGrant);
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var discovery = scope.ServiceProvider.GetRequiredService<GovernanceDiscovery>();
        var operation = scope.ServiceProvider.GetRequiredService<NativeOperation>();
        var token = TestContext.Current.CancellationToken;
        var original = await discovery.DiscoverAsync(change.Actor, request, token);
        Assert.Equal(change.Actor, Assert.Single(original.Users));
        await change.ApplyAsync(scenario);
        var before = await scenario.SnapshotAsync();
        trace.Arm(database, change.Actor, change.Candidate);
        await using (var transaction = await CeremonyTransaction.OpenGovernanceAsync(database, operation, original, token))
        {
            await Assert.ThrowsAsync<FactorUnavailableException>(() => discovery.RequireUnchangedAsync(transaction, change.Actor, request));
        }
        trace.Require(original);
        Assert.Equal(before, await scenario.SnapshotAsync());
        var fresh = await discovery.DiscoverAsync(change.Actor, request, token);
        Assert.Equal(new[] { change.Actor, change.Candidate }.Order(StringComparer.Ordinal), fresh.Users);
        trace.Arm(database, change.Actor, change.Candidate);
        await using (var transaction = await CeremonyTransaction.OpenGovernanceAsync(database, operation, fresh, token))
        {
            await discovery.RequireUnchangedAsync(transaction, change.Actor, request);
        }
        trace.Require(fresh);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }
    private const string SelfRemoval = "self-removal";
    private const string CrossRemoval = "cross-removal";
    private const string PlatformRole = "PlatformAdministrator";
    private const string RemovedEvent = "AdministratorBundleRemoved";
    private const string Accepted = "Accepted";
    private const int CompetingWriters = 2;
    public static IEnumerable<TheoryDataRow<string, string>> NodeCases => GovernanceRows.For(SelfRemoval, CrossRemoval);

    /// <summary>Two actual command processes contend behind an observed singleton lock; exactly one destructive intent commits.</summary>
    [Theory, MemberData(nameof(NodeCases))]
    public async Task Given_TwoGovernanceNodes_When_DestructiveCommandsContend_Then_OnlyOneCanRemoveAuthority(string provider, string condition)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var other = await GovernanceAccounts.AddAsync(scenario);
        var secondSession = await GovernanceAccounts.LoginAsync(scenario, other);
        var role = await GovernanceCommands.RoleAsync(scenario, PlatformRole);
        var firstTarget = condition == SelfRemoval ? scenario.ActorId : other;
        var secondTarget = condition == SelfRemoval ? other : scenario.ActorId;
        var firstIntent = await scenario.RequestAsync(GovernanceAction.RemoveBundle, firstTarget, role);
        var secondIntent = await scenario.RequestAsync(GovernanceAction.RemoveBundle, secondTarget, role);
        var firstCode = await scenario.UnusedCodeAsync();
        var secondCode = await scenario.UnusedCodeAsync(other, FactorScenario.Seed);
        await using var first = new GovernanceDriver();
        await using var second = new GovernanceDriver();
        await using (var held = await GovernanceLock.AcquireAsync(scenario.Journey.Lease))
        {
            await GovernanceDriverInput.ExecuteAsync(first.Input, scenario, provider, firstIntent, scenario.Session,
                scenario.Password, firstCode, false, first.Token);
            await GovernanceDriverInput.ExecuteAsync(second.Input, scenario, provider, secondIntent, secondSession,
                GovernanceAccounts.Password(scenario), secondCode, false, second.Token);
            using var firstReady = await first.ReadFrameAsync(GovernanceDriver.Ready);
            using var secondReady = await second.ReadFrameAsync(GovernanceDriver.Ready);
            await held.ObserveWaitersAsync(scenario.Journey.Lease, CompetingWriters);
        }
        var outcomes = await Task.WhenAll(first.CompleteCommandAsync(), second.CompleteCommandAsync());
        Assert.Single(outcomes, value => value == GovernanceOutcome.Accepted);
        Assert.Single(outcomes, value => value is null);
        await RequireSingleRemovalAsync(scenario, role);
    }

    /// <summary>Queries complete native surviving membership and attributed audit instead of accepting child exit status as mutation evidence.</summary>
    private static async Task RequireSingleRemovalAsync(GovernanceScenario scenario, string role)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(1, await database.Set<AdministratorGovernance>().Select(row => row.AuthorityRevision).SingleAsync(token));
        var remaining = await database.UserRoles.Where(row => row.RoleId == role).Select(row => row.UserId).SingleAsync(token);
        var observation = await scenario.ReadAsync(remaining);
        Assert.Equal(remaining, Assert.Single(observation.UsableAdministrators));
        var audit = Assert.Single(await database.Set<SecurityAuditIntent>().Where(row => row.Event == RemovedEvent).ToArrayAsync(token));
        Assert.Equal(Accepted, audit.Outcome);
        Assert.False(await database.UserRoles.AnyAsync(row => row.UserId == audit.TargetId && row.RoleId == role, token));
        Assert.True(await database.Users.AnyAsync(row => row.Id == audit.ActorId, token));
    }
    public static IEnumerable<TheoryDataRow<string, string>> Cancellations => GovernanceRows.For(
        DeadlineCase, CancellationCase);

    /// <summary>Native singleton contention terminates only through its unchanged production deadline or explicitly cancelled caller.</summary>
    [Theory, MemberData(nameof(Cancellations))]
    public async Task Given_HeldRequiredLock_When_OperationDeadlineOrCancellationExpires_Then_AllOwnershipIsReleased(string provider, string variant)
    {
        var evidence = new GovernanceCancellationEvidence();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(evidence)));
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RestoreGrant);
        var code = await scenario.UnusedCodeAsync();
        var before = await scenario.SnapshotAsync();
        await RequireSingletonTerminationAsync(scenario, request, code, evidence, provider, variant);
        await scenario.Journey.Lease.AssertRuntimeDrainedAsync();
        Assert.Equal(before, await scenario.SnapshotAsync());
        await using var reacquired = await GovernanceOperation.OpenAsync(scenario, request);
    }

    /// <summary>Retains the external blocker until the owned subject has terminated, preventing lock release from masquerading as cancellation.</summary>
    private static async Task RequireSingletonTerminationAsync(GovernanceScenario scenario, GovernanceRequest request, string code,
        GovernanceCancellationEvidence evidence, string provider, string variant)
    {
        const string Caller = CancellationCase;
        const string Deadline = DeadlineCase;
        const string SqlServer = "SqlServer";
        Assert.True(variant is Caller or Deadline);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var probe = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var scope = scenario.CreateScope();
        await using var blocker = await GovernanceLock.AcquireAsync(scenario.Journey.Lease);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var watch = Stopwatch.StartNew();
        evidence.Arm(database, GovernanceCancellationEvidence.Singleton, provider == SqlServer, () => caller.IsCancellationRequested, watch);
        var subject = scope.ServiceProvider.GetRequiredService<GovernanceService>()
            .ExecuteAsync(request, scenario.Session, scenario.Password, code, caller.Token);
        var wait = blocker.ObserveOwnedAsync(scenario.Journey.Lease, 1, probe.Token);
        await GovernanceWaitTermination.RequireAsync(subject, wait, caller, probe, evidence, variant == Caller, true, watch, blocker.DisposeAsync);
    }
}
