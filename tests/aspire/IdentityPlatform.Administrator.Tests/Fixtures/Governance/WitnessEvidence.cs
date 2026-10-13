using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Checks durable authority and mandatory security outcomes with no whole-snapshot read across a deliberately held commit.</summary>
internal static class WitnessEvidence
{
    internal const string AccountStage = "%UPDATE%AdministratorAuthentication%";
    internal const string GovernanceStage = "%UPDATE%AdministratorGovernance%";
    private const string Platform = "PlatformAdministrator";
    private const string Removed = "AdministratorBundleRemoved";
    private const string Accepted = "Accepted";
    private const string RecoveryState = "RecoveryRequired";
    private const int EnrollmentAndGovernanceReplay = 2;
    private const int LockedFailures = 5;
    private const int DrainSeconds = 10;

    /// <summary>Captures actual expected versions for self-demotion while the witness still provides native usable authority.</summary>
    internal static async Task<GovernanceRequest> RequestAsync(GovernanceScenario scenario) =>
        await scenario.RequestAsync(GovernanceAction.RemoveBundle, scenario.ActorId, await GovernanceCommands.RoleAsync(scenario, Platform));

    /// <summary>Owns an independent complete service invocation under the caller's cancellable lifetime.</summary>
    internal static async Task<GovernanceOutcome> ExecuteAsync(GovernanceScenario scenario, GovernanceRequest request, CancellationToken token)
    {
        var code = await scenario.UnusedCodeAsync();
        await using var scope = scenario.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GovernanceService>().ExecuteAsync(request, scenario.Session, scenario.Password, code, token);
    }

    /// <summary>Reads only witness rows when governance is paused, avoiding SQL Server waits on its uncommitted actor/audit rows.</summary>
    internal static async Task RequireSecurityAsync(GovernanceScenario scenario, WitnessSecurity witness, string variant)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var user = await database.Users.AsNoTracking().SingleAsync(row => row.Id == witness.Id, token);
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(row => row.UserId == witness.Id, token);
        var authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(row => row.UserId == witness.Id, token);
        if (variant == WitnessSecurity.Lockout)
        {
            Assert.Equal(LockedFailures, authentication.Failures);
            Assert.True(authentication.LockoutUntil > scenario.Journey.Clock.Now.ToUnixTimeSeconds());
            Assert.Equal(authentication.LockoutUntil, user.LockoutEnd?.ToUnixTimeSeconds());
        }
        else if (variant is WitnessSecurity.Recovery or WitnessSecurity.RestrictedRecovery)
        {
            Assert.Equal(RecoveryState, enrollment.State);
        }
        else if (variant == WitnessSecurity.Disable)
        {
            Assert.False(user.IsEnabled);
        }
        else if (variant == WitnessSecurity.Suspension)
        {
            Assert.True(enrollment.Suspended);
        }
        else
        {
            Assert.False(await database.Set<AdministratorFactor>().AnyAsync(row => row.UserId == witness.Id, token));
        }
        var validation = scope.ServiceProvider.GetRequiredService<JourneySessions>().ValidateAsync(witness.Id, witness.Session, token);
        if (variant == WitnessSecurity.FactorChange)
        {
            await Assert.ThrowsAsync<FactorUnavailableException>(() => validation);
        }
        else
        {
            Assert.Null(await validation);
        }
    }

    /// <summary>Requires exact committed membership, semantic revision, attributable audit and actor proof effects after all writers finish.</summary>
    internal static async Task RequireAuthorityAsync(GovernanceScenario scenario, GovernanceRequest request, bool accepted)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(!accepted, await database.UserRoles.AnyAsync(row => row.UserId == scenario.ActorId && row.RoleId == request.RoleId, token));
        Assert.Equal(accepted ? 1 : 0, await database.Set<AdministratorGovernance>().Select(row => row.AuthorityRevision).SingleAsync(token));
        var audits = await database.Set<SecurityAuditIntent>().Where(row => row.Event == Removed).ToArrayAsync(token);
        Assert.Equal(accepted ? 1 : 0, audits.Length);
        foreach (var audit in audits)
        {
            Assert.Equal(scenario.ActorId, audit.ActorId);
            Assert.Equal(scenario.ActorId, audit.TargetId);
            Assert.Equal(Accepted, audit.Outcome);
        }
        Assert.Equal(accepted ? EnrollmentAndGovernanceReplay : 1, await database.Set<AdministratorReplay>().CountAsync(row => row.UserId == scenario.ActorId, token));
    }

    /// <summary>Separates a real native wait from a completed mandatory writer while governance still cannot execute its native commit.</summary>
    internal static async Task<bool> RequireOrderAsync(GovernanceScenario scenario, WitnessSecurity witness, NativeCommitGate gate,
        Task write, Task governance, Task observation)
    {
        var first = await Task.WhenAny(observation, write, governance);
        Assert.NotSame(governance, first);
        Assert.False(gate.Released);
        if (first == observation)
        {
            await observation;
            Assert.False(write.IsCompleted);
            return false;
        }
        await write;
        await RequireSecurityAsync(scenario, witness, WitnessSecurity.Lockout);
        Assert.False(gate.Released);
        Assert.False(governance.IsCompleted);
        return true;
    }

    /// <summary>Joins all owned tasks after cancellation without accepting faults or timeout as successful race evidence.</summary>
    internal static async Task DrainAsync(params Task?[] tasks)
    {
        var all = Task.WhenAll(tasks.OfType<Task>());
        _ = await Record.ExceptionAsync(() => all.WaitAsync(TimeSpan.FromSeconds(DrainSeconds)));
        Assert.True(all.IsCompleted);
    }
}
