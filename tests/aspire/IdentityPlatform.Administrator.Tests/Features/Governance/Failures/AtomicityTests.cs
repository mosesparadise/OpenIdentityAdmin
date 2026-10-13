using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Interrupts actual protected writes before commit and compares every native, product, proof and authority surface.</summary>
[Collection("administrator-bootstrap")]
public sealed class AtomicityTests(BootstrapFixture fixture)
{
    private const string AuditDenied = "audit-insert-denied";
    private const string Concurrency = "manager-concurrency-failure";
    private const string SessionDeath = "database-session-terminated";
    private const string Cancellation = "caller-cancelled";
    private const string ProcessDeath = "child-process-killed";
    private const string SqlServer = "SqlServer";
    private const string Accepted = "Accepted";
    private const string CriticalDecision = "CriticalDecision";
    private const string AuditDecision = "governance-audit";
    private const string AuditBypass = "governance-audit-bypass";
    private const int DrainSeconds = 10;
    private const int UnavailableExit = 2;
    public static IEnumerable<TheoryDataRow<string, string>> Cases => GovernanceRows.For(AuditDenied)
        .Select(row => row.WithTrait(CriticalDecision, AuditDecision))
        .Concat(GovernanceRows.For(Concurrency, SessionDeath, Cancellation, ProcessDeath));

    /// <summary>Every selected fault occurs inside a real command; no partial state or successful completion survives owned cleanup.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_NativeWritesBeforeCommit_When_OperationFails_Then_AllGovernedStateRollsBack(string provider, string condition)
    {
        var native = new GovernanceNativeConcurrency();
        var failure = new GovernanceAuditFailure();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(native, failure)));
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.Assign);
        var code = await scenario.UnusedCodeAsync();
        var before = await scenario.SnapshotAsync();
        switch (condition)
        {
            case AuditDenied:
                await RequireAuditFailureAsync(scenario, request, code, failure, provider);
                break;
            case Concurrency:
                await RequireConcurrencyAsync(scenario, request, code, native);
                break;
            case Cancellation:
                await RequireCancellationAsync(scenario, request, code, failure, provider);
                break;
            case SessionDeath:
            case ProcessDeath:
                await RequireChildFailureAsync(scenario, request, code, provider, condition);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(condition));
        }
        await scenario.Journey.Lease.AssertRuntimeDrainedAsync();
        Assert.Equal(before, await scenario.SnapshotAsync());
        await using var reacquired = await GovernanceLock.AcquireAsync(scenario.Journey.Lease);
    }

    /// <summary>Requires the actual provider's INSERT denial, or proves a durable unaudited transition before emitting a mutation marker.</summary>
    private static async Task RequireAuditFailureAsync(GovernanceScenario scenario, GovernanceRequest request, string code,
        GovernanceAuditFailure observer, string provider)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var count = await database.Set<SecurityAuditIntent>().CountAsync(row => row.Outcome == Accepted, token);
        observer.Arm(provider == SqlServer);
        await scenario.Journey.Lease.AuditPermissionAsync(false, token);
        try
        {
            var outcome = GovernanceOutcome.Denied;
            var exception = await Record.ExceptionAsync(async () => outcome = await scenario.ExecuteAsync(request, code: code));
            if (outcome == GovernanceOutcome.Accepted)
            {
                await GovernanceUnauditedCommit.RequireAsync(scenario, request, count, token);
                Assert.Fail(AuditBypass);
            }
            Assert.IsType<GovernanceUnavailableException>(exception);
            Assert.True(observer.PermissionDenied);
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(DrainSeconds));
            await scenario.Journey.Lease.AuditPermissionAsync(true, cleanup.Token);
        }
    }

    /// <summary>Injects a stale expected native stamp only at the target membership write, after observing reserved replay inside that transaction.</summary>
    private static async Task RequireConcurrencyAsync(GovernanceScenario scenario, GovernanceRequest request, string code,
        GovernanceNativeConcurrency native)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var replay = await database.Set<AdministratorReplay>().CountAsync(row => row.UserId == scenario.ActorId, TestContext.Current.CancellationToken);
        native.Arm(scenario.ActorId, request.UserId!, request.RoleId!, replay);
        await Assert.ThrowsAsync<GovernanceUnavailableException>(() => scenario.ExecuteAsync(request, code: code));
        Assert.True(native.ObservedTarget && native.ObservedReplay && native.ObservedConcurrency);
    }

    /// <summary>Kills the executing child or its exact blocked native audit session, after independently observing the real precommit waiter.</summary>
    private static async Task RequireChildFailureAsync(GovernanceScenario scenario, GovernanceRequest request, string code,
        string provider, string condition)
    {
        await using var blocked = await GovernanceLock.AcquireAsync(scenario.Journey.Lease, audit: true);
        await using var child = new GovernanceDriver();
        await GovernanceDriverInput.ExecuteAsync(child.Input, scenario, provider, request, scenario.Session,
            scenario.Password, code, false, child.Token);
        using var ready = await child.ReadFrameAsync(GovernanceDriver.Ready);
        await blocked.ObserveWaitersAsync(scenario.Journey.Lease, 1);
        if (condition == ProcessDeath)
        {
            await child.KillAsync();
        }
        else
        {
            await blocked.TerminateAuditWaiterAsync(scenario.Journey.Lease);
            using var unavailable = await child.ReadFrameAsync(GovernanceDriver.Unavailable);
            await child.RequireExitAsync(UnavailableExit);
        }
    }

    /// <summary>Cancels the actual caller only after blocked audit evidence and joins before releasing the scope or asserting rollback.</summary>
    private static async Task RequireCancellationAsync(GovernanceScenario scenario, GovernanceRequest request, string code,
        GovernanceAuditFailure observer, string provider)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var scope = scenario.CreateScope();
        await using var blocked = await GovernanceLock.AcquireAsync(scenario.Journey.Lease, audit: true);
        observer.Arm(provider == SqlServer, () => cancellation.IsCancellationRequested);
        var task = scope.ServiceProvider.GetRequiredService<GovernanceService>()
            .ExecuteAsync(request, scenario.Session, scenario.Password, code, cancellation.Token);
        try
        {
            await blocked.ObserveWaitersAsync(scenario.Journey.Lease, 1);
            Assert.False(task.IsCompleted || cancellation.IsCancellationRequested);
            await cancellation.CancelAsync();
            var exception = await Record.ExceptionAsync(() => task.WaitAsync(TimeSpan.FromSeconds(DrainSeconds)));
            Assert.True(task.IsCompleted && observer.CallerCancelled);
            Assert.True(exception is OperationCanceledException or GovernanceUnavailableException);
        }
        finally
        {
            await GovernanceWaitTermination.CleanupSubjectAsync(task, cancellation, blocked.DisposeAsync);
        }
    }
}
