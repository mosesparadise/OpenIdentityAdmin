using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Owns an independent real logout/governance command or explicitly test-owned external profile change.</summary>
internal sealed class NativeAuthorityMutation(AsyncServiceScope scope, Func<Task> execute, Action<PlatformDbContext> arm, Guid[] auditIds) : IAsyncDisposable
{
    private const int ExternalMutationSeconds = 5;
    private const string LogoutEvent = "administrator-logout";
    private const string BundleRemovedEvent = "AdministratorBundleRemoved";
    private const string CeremonyAccepted = "accepted";
    private const string GovernanceAccepted = "Accepted";
    internal enum Change
    {
        Session, Permissions, Registration
    }

    /// <summary>Prepares immutable versions and a fresh factor before either contender holds a lock; setup is never credited as the mutation.</summary>
    internal static async Task<NativeAuthorityMutation> PrepareAsync(AdministratorProtocolScenario scenario, Change change, NativeAuthorityRaceProbe probe)
    {
        GovernanceRequest? request = null;
        string? code = null;
        if (change == Change.Permissions)
        {
            request = await GovernanceCommands.PrepareAsync(scenario.Account, GovernanceCommands.Remove);
            code = await scenario.Account.UnusedCodeAsync();
        }
        var auditIds = await ReadAuditIdsAsync(scenario);
        var scope = scenario.Account.CreateScope();
        var services = scope.ServiceProvider;
        return new(scope, () => ExecuteAsync(services, scenario, change, request, code), probe.ArmMutation, auditIds);
    }

    /// <summary>Invokes the selected real command with immutable prepared proof, or the explicitly external profile writer.</summary>
    private static async Task ExecuteAsync(IServiceProvider services, AdministratorProtocolScenario scenario, Change change,
        GovernanceRequest? request, string? code)
    {
        var token = TestContext.Current.CancellationToken;
        switch (change)
        {
            case Change.Session:
                Assert.True(await services.GetRequiredService<JourneyLogout>().RevokeAsync(scenario.Account.Session, token));
                break;
            case Change.Permissions:
                Assert.Equal(GovernanceOutcome.Accepted, await services.GetRequiredService<GovernanceService>()
                    .ExecuteAsync(request!, scenario.Account.Session, scenario.Account.Password, code!, token));
                break;
            case Change.Registration:
                await DisableProfileAsync(services.GetRequiredService<PlatformDbContext>(), token);
                break;
        }
    }

    /// <summary>Arms only its exact context before starting the independently committing operation once.</summary>
    internal Task InvokeAsync()
    {
        arm(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
        return execute();
    }

    /// <summary>Models an external profile change using governance-before-profile locking; this is not a supported audited operator command.</summary>
    private static async Task DisableProfileAsync(PlatformDbContext database, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(ExternalMutationSeconds));
        token = deadline.Token;
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        await new CeremonyLocks(database).AcquireAsync(new CeremonyLockPlan(true, false, [], []), token);
        Assert.Equal(1, await database.Set<AdministratorProtocolProfile>().Where(row => row.Id == AdministratorProtocolProfile.SingletonId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.LockRevision, row => row.LockRevision + 1)
                .SetProperty(row => row.Revision, row => row.Revision + 1).SetProperty(row => row.Enabled, false), token));
        await transaction.CommitAsync(token);
    }

    /// <summary>Rereads the actual committed loss through a fresh untracked context after every contender has drained.</summary>
    internal async Task RequirePersistedAsync(AdministratorProtocolScenario scenario, Change change)
    {
        await using var fresh = scenario.Account.CreateScope();
        var database = fresh.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        switch (change)
        {
            case Change.Session:
                Assert.True(await database.Set<AdministratorSession>().AsNoTracking()
                    .Where(row => row.PublicId == scenario.Account.Session.SessionId).Select(row => row.Revoked).SingleAsync(token));
                break;
            case Change.Permissions:
                Assert.False(await database.UserRoles.AsNoTracking().AnyAsync(row => row.UserId == scenario.Account.ActorId, token));
                break;
            case Change.Registration:
                Assert.False(await database.Set<AdministratorProtocolProfile>().AsNoTracking().Select(row => row.Enabled).SingleAsync(token));
                break;
        }
        await RequireAuditAsync(database, scenario, change, token);
    }

    /// <summary>Snapshots independent audit identities after fixture preparation and before either operation begins.</summary>
    private static async Task<Guid[]> ReadAuditIdsAsync(AdministratorProtocolScenario scenario)
    {
        await using var scope = scenario.Account.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>()
            .AsNoTracking().Select(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Requires the actual product mutation audit; the external profile writer cannot pretend to be an audited operator command.</summary>
    private async Task RequireAuditAsync(PlatformDbContext database, AdministratorProtocolScenario scenario, Change change, CancellationToken token)
    {
        var rows = await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => !auditIds.Contains(row.Id)).ToArrayAsync(token);
        if (change == Change.Registration)
        {
            Assert.Empty(rows);
            return;
        }
        var audit = Assert.Single(rows);
        Assert.Equal(change == Change.Session ? LogoutEvent : BundleRemovedEvent, audit.Event);
        Assert.Equal(change == Change.Session ? CeremonyAccepted : GovernanceAccepted, audit.Outcome);
        Assert.True(audit.ActorId == scenario.Account.ActorId && audit.TargetId == scenario.Account.ActorId);
    }

    /// <summary>Releases the mutation scope only after orchestration has joined the native operation.</summary>
    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
