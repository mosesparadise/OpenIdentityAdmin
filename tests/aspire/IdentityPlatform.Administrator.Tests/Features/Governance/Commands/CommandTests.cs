using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Commands;

/// <summary>Verifies all supported transitions, including intentional self-revocation, against independent stored outcomes.</summary>
[Collection("administrator-bootstrap")]
public sealed class CommandTests(BootstrapFixture fixture)
{
    public static IEnumerable<TheoryDataRow<string, string>> Commands => GovernanceRows.For(GovernanceCommands.Cases);
    private const string BundleAssignedEvent = "AdministratorBundleAssigned";
    private const string BundleRemovedEvent = "AdministratorBundleRemoved";
    private const string GrantAddedEvent = "AdministratorGrantAdded";
    private const string GrantRemovedEvent = "AdministratorGrantRemoved";
    private const string EnabledEvent = "AdministratorEnabled";
    private const string DisabledEvent = "AdministratorDisabled";
    private const string SuspendedEvent = "AdministratorSuspended";
    private const string ResumedEvent = "AdministratorResumed";
    private const string Accepted = "Accepted";
    private const string AuditPrefix = "Administrator";
    private const string PermissionType = "identityplatform.permission";

    /// <summary>Every supported command proves its real effect, one version advance and correctly attributed accepted audit.</summary>
    [Theory, MemberData(nameof(Commands))]
    public async Task Given_CurrentActorAndFreshBoundProof_When_SupportedCommandCommits_Then_StateVersionAndAuditAgree(string provider, string command)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, command);
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var generation = await database.Set<AdministratorAuthentication>().Where(row => row.UserId == scenario.ActorId)
            .Select(row => row.Generation).SingleAsync(token);
        var auditIds = await database.Set<SecurityAuditIntent>().Select(row => row.Id).ToArrayAsync(token);
        var result = await scenario.ExecuteAsync(request);
        Assert.Equal(GovernanceOutcome.Accepted, result);
        await RequireEffectAsync(database, request);
        Assert.Equal(1, await database.Set<AdministratorGovernance>().Select(row => row.AuthorityRevision).SingleAsync(token));
        var audit = Assert.Single(await database.Set<SecurityAuditIntent>().Where(row => !auditIds.Contains(row.Id)).ToArrayAsync(token));
        Assert.Equal(scenario.ActorId, audit.ActorId);
        Assert.Equal(request.UserId ?? request.RoleId, audit.TargetId);
        Assert.Equal(Accepted, audit.Outcome);
        Assert.StartsWith(AuditPrefix, audit.Event, StringComparison.Ordinal);
        Assert.Equal(ExpectedEvent(request.Action), audit.Event);
        Assert.NotEqual(Guid.Empty, audit.CorrelationId);
        Assert.Equal(2, await database.Set<AdministratorReplay>().CountAsync(token));
        await RequireRevocationAsync(database, request, scenario, generation);
    }

    /// <summary>Already-satisfied intent neither verifies credentials nor changes locks, budgets, proof, audit or native versions.</summary>
    [Theory, MemberData(nameof(Commands))]
    public async Task Given_AlreadySatisfiedCommand_When_Requested_Then_NoChangePreservesProofAuditAndVersion(string provider, string command)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, command, true);
        var before = await scenario.SnapshotAsync();
        Assert.Equal(GovernanceOutcome.NoChange, await scenario.ExecuteAsync(request));
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    private const string RenameRole = "rename-reserved-role";
    private const string DeleteRole = "delete-reserved-role";
    private const string InvalidGrant = "unknown-or-out-of-bundle-grant";
    private const string DeleteAccount = "delete-account";
    private const string UnknownPermission = "unknown.permission";
    private const string OutOfBundlePermission = "keys.read";
    private const int UnsupportedAction = int.MaxValue;
    public static IEnumerable<TheoryDataRow<string, string>> UnsupportedCases =>
        GovernanceRows.For(RenameRole, DeleteRole, InvalidGrant, DeleteAccount);

    /// <summary>Unsupported lifecycle commands have no dispatch value, while an otherwise finite grant cannot escape its reserved bundle.</summary>
    [Theory, MemberData(nameof(UnsupportedCases))]
    public async Task Given_PrivilegedActor_When_UnsupportedAuthorityChangeIsRequested_Then_NoWriteOccurs(string provider, string condition)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var valid = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RemoveGrant);
        var before = await scenario.SnapshotAsync();
        if (condition == InvalidGrant)
        {
            Assert.Null(GovernanceRequest.Create(GovernanceAction.AddGrant, valid.Versions, roleId: valid.RoleId, permission: UnknownPermission));
            var outsideBundle = GovernanceRequest.Create(GovernanceAction.AddGrant, valid.Versions,
                roleId: valid.RoleId, permission: OutOfBundlePermission);
            Assert.NotNull(outsideBundle);
            await Assert.ThrowsAsync<GovernanceUnavailableException>(() => scenario.ExecuteAsync(outsideBundle));
        }
        else
        {
            Assert.Contains(condition, new[] { RenameRole, DeleteRole, DeleteAccount });
            Assert.False(Enum.TryParse<GovernanceAction>(condition, out _));
            Assert.Null(GovernanceRequest.Create((GovernanceAction)UnsupportedAction, valid.Versions, roleId: valid.RoleId));
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Queries native membership/grant rows and account flags without consulting the production outcome evaluator.</summary>
    private static async Task RequireEffectAsync(PlatformDbContext database, GovernanceRequest request)
    {
        var token = TestContext.Current.CancellationToken;
        if (request.Action is GovernanceAction.AssignBundle or GovernanceAction.RemoveBundle)
        {
            Assert.Equal(request.Action == GovernanceAction.AssignBundle,
                await database.UserRoles.AnyAsync(row => row.UserId == request.UserId && row.RoleId == request.RoleId, token));
            return;
        }
        if (request.Action is GovernanceAction.AddGrant or GovernanceAction.RemoveGrant)
        {
            Assert.Equal(request.Action == GovernanceAction.AddGrant,
                await database.RoleClaims.AnyAsync(row => row.RoleId == request.RoleId && row.ClaimType == PermissionType && row.ClaimValue == GovernanceCommands.Permission, token));
            return;
        }
        if (request.Action is GovernanceAction.EnableAccount or GovernanceAction.DisableAccount)
        {
            Assert.Equal(request.Action == GovernanceAction.EnableAccount,
                await database.Users.Where(row => row.Id == request.UserId).Select(row => row.IsEnabled).SingleAsync(token));
        }
        else
        {
            Assert.Equal(request.Action == GovernanceAction.SuspendAccount,
                await database.Set<AdministratorEnrollment>().Where(row => row.UserId == request.UserId).Select(row => row.Suspended).SingleAsync(token));
        }
    }

    /// <summary>Destructive account transitions revoke the original session and advance authentication generation atomically.</summary>
    private static async Task RequireRevocationAsync(PlatformDbContext database, GovernanceRequest request, GovernanceScenario scenario, long originalGeneration)
    {
        if (request.Action is not (GovernanceAction.DisableAccount or GovernanceAction.SuspendAccount))
        {
            return;
        }
        var token = TestContext.Current.CancellationToken;
        Assert.True(await database.Set<AdministratorSession>().Where(row => row.PublicId == scenario.Grant.SessionId).Select(row => row.Revoked).SingleAsync(token));
        Assert.Equal(originalGeneration + 1, await database.Set<AdministratorAuthentication>().Where(row => row.UserId == request.UserId).Select(row => row.Generation).SingleAsync(token));
    }

    /// <summary>Declares independent fixed audit expectations for each command rather than importing the production writer.</summary>
    private static string ExpectedEvent(GovernanceAction action) => action switch
    {
        GovernanceAction.AssignBundle => BundleAssignedEvent,
        GovernanceAction.RemoveBundle => BundleRemovedEvent,
        GovernanceAction.AddGrant => GrantAddedEvent,
        GovernanceAction.RemoveGrant => GrantRemovedEvent,
        GovernanceAction.EnableAccount => EnabledEvent,
        GovernanceAction.DisableAccount => DisabledEvent,
        GovernanceAction.SuspendAccount => SuspendedEvent,
        GovernanceAction.ResumeAccount => ResumedEvent,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };
    private const string Membership = "remove-membership";
    private const string Disable = "disable-self";
    private const string Suspend = "suspend-self";
    private const string SharedGrant = "remove-shared-platform-grant";
    private const string Platform = "PlatformAdministrator";
    private const string CriticalDecision = "CriticalDecision";
    private const string LastAdministrator = "governance-last-administrator";
    private const string BypassMarker = "governance-last-administrator-bypass";
    public static IEnumerable<TheoryDataRow<string, string>> SurvivorCases => GovernanceRows.For(Membership)
        .Select(row => row.WithTrait(CriticalDecision, LastAdministrator))
        .Concat(GovernanceRows.For(Disable, Suspend, SharedGrant));

    /// <summary>A voluntary destructive command cannot leave zero complete usable administrators, even with multiple members of an incomplete shared role.</summary>
    [Theory, MemberData(nameof(SurvivorCases))]
    public async Task Given_LastUsableAdministrator_When_VoluntaryAuthorityIsDestroyed_Then_CommandDenies(string provider, string variant)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var role = await GovernanceCommands.RoleAsync(scenario, Platform);
        if (variant == SharedGrant)
        {
            _ = await GovernanceAccounts.AddAsync(scenario);
        }
        var request = variant switch
        {
            Membership => await scenario.RequestAsync(GovernanceAction.RemoveBundle, scenario.ActorId, role),
            Disable => await scenario.RequestAsync(GovernanceAction.DisableAccount, scenario.ActorId),
            Suspend => await scenario.RequestAsync(GovernanceAction.SuspendAccount, scenario.ActorId),
            SharedGrant => await scenario.RequestAsync(GovernanceAction.RemoveGrant, roleId: role, permission: GovernanceCommands.Permission),
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };
        var before = await scenario.SnapshotAsync();
        var durable = variant == Membership ? await GovernanceDurableMutationEvidence.CaptureAsync(scenario) : null;
        var outcome = GovernanceOutcome.Denied;
        var exception = await Record.ExceptionAsync(async () => outcome = await scenario.ExecuteAsync(request));
        if (variant == Membership && outcome == GovernanceOutcome.Accepted)
        {
            Assert.NotNull(durable);
            await GovernanceDurableMutationEvidence.RequireMembershipCommitAsync(scenario, request, durable);
            await GovernanceDurableMutationEvidence.RequireZeroPlatformSurvivorsAsync(scenario, request, durable);
            Assert.Fail(BypassMarker);
        }
        Assert.IsType<GovernanceUnavailableException>(exception);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }
}
