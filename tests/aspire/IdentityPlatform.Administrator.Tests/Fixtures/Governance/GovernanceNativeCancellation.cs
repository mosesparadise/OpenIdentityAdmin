using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Exercises the actual native managers with their real original transaction cancellation token.</summary>
internal static class GovernanceNativeCancellation
{
    private const string SqlServer = "SqlServer";
    private const string Specialist = "IdentityAdministrator";

    /// <summary>Preloads only fixture input, then blocks real native validation/persistence before cancelling the explicit caller.</summary>
    internal static async Task RequireAsync(GovernanceScenario scenario, GovernanceCancellationEvidence evidence, string provider, bool role)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var probe = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var user = await database.Users.SingleAsync(row => row.Id == scenario.ActorId, token);
        var nativeRole = await database.Roles.SingleAsync(row => row.Name == Specialist, token);
        await using var blocker = await GovernanceNativeBlock.AcquireAsync(scenario.Journey.Lease, role);
        await using var transaction = await CeremonyTransaction.OpenGovernanceAsync(database,
            scope.ServiceProvider.GetRequiredService<NativeOperation>(), new CeremonyLockPlan(true, false, [], []), caller.Token);
        evidence.Arm(database, role ? GovernanceCancellationEvidence.Roles : GovernanceCancellationEvidence.Users,
            provider == SqlServer, () => caller.IsCancellationRequested, token: transaction.Token);
        var subject = Start(scope.ServiceProvider, database, transaction, user, nativeRole, role);
        var wait = blocker.ObserveAsync(scenario.Journey.Lease, probe.Token);
        await GovernanceWaitTermination.RequireAsync(subject, wait, caller, probe, evidence, true, false, failedCleanupRelease: blocker.DisposeAsync);
    }

    /// <summary>Checks actual manager ownership before invoking the real native update orchestration and validators.</summary>
    private static Task<IdentityResult> Start(IServiceProvider services, PlatformDbContext database, CeremonyTransaction transaction,
        PlatformUser user, IdentityRole role, bool roleOperation)
    {
        if (roleOperation)
        {
            var manager = Assert.IsType<GovernanceRoleManager>(services.GetRequiredService<RoleManager<IdentityRole>>());
            manager.RequireOperation(database, transaction.Token);
            return manager.UpdateAsync(role);
        }
        var users = Assert.IsType<AdministratorUserManager>(services.GetRequiredService<UserManager<PlatformUser>>());
        users.RequireOperation(database, transaction.Token);
        return users.UpdateAsync(user);
    }
}
