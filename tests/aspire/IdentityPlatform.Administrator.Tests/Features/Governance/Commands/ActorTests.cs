using IdentityPlatform.Administrator.Tests.Features.Governance.Policy;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Commands;

/// <summary>Requires current stored actor permissions and a truthful full session independently of population eligibility.</summary>
[Collection("administrator-bootstrap")]
public sealed class ActorTests(BootstrapFixture fixture)
{
    private const string WriteUsers = "users.write";
    private const string RolePermissions = "roles.permissions";
    private const string DisableUsers = "users.disable";
    private const string AssignAdministrators = "administrators.assign";
    private const string CriticalDecision = "CriticalDecision";
    private const string CurrentGrants = "governance-current-grants";
    private const string BypassMarker = "governance-current-grants-bypass";
    public static IEnumerable<TheoryDataRow<string, string>> Permissions => GovernanceRows.For(AssignAdministrators)
        .Select(row => row.WithTrait(CriticalDecision, CurrentGrants)).Concat(GovernanceRows.For(WriteUsers, RolePermissions, DisableUsers));

    /// <summary>Every required permission is checked before writes; a specialist actor cannot borrow a separate survivor's assignment authority.</summary>
    [Theory, MemberData(nameof(Permissions))]
    public async Task Given_OtherwiseCurrentActor_When_RequiredPermissionIsMissing_Then_CommandDenies(string provider, string permission)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await PrepareMissingPermissionAsync(scenario, permission);
        var before = await scenario.SnapshotAsync();
        var result = GovernanceOutcome.Denied;
        try
        {
            result = await scenario.ExecuteAsync(request);
        }
        catch (GovernanceUnavailableException) { }
        if (result == GovernanceOutcome.Accepted)
        {
            await RequireUnauthorizedCommitAsync(scenario, request);
            Assert.Fail(BypassMarker);
        }
        Assert.Equal(GovernanceOutcome.Denied, result);
        Assert.Equal(before, await scenario.SnapshotAsync());
        await RequireDirectBoundaryAsync(scenario, request);
    }

    private const string AssignedEvent = "AdministratorBundleAssigned";
    private const string Accepted = "Accepted";

    /// <summary>Requires an actual unauthorized membership, version and attributed audit before the mutation marker can claim a durable bypass.</summary>
    private static async Task RequireUnauthorizedCommitAsync(GovernanceScenario scenario, GovernanceRequest request)
    {
        Assert.Equal(GovernanceAction.AssignBundle, request.Action);
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        Assert.True(await database.UserRoles.AnyAsync(row => row.UserId == request.UserId && row.RoleId == request.RoleId, token));
        Assert.Equal(request.Versions.AuthorityRevision + 1,
            await database.Set<AdministratorGovernance>().Select(row => row.AuthorityRevision).SingleAsync(token));
        var audit = Assert.Single(await database.Set<SecurityAuditIntent>().Where(row => row.Event == AssignedEvent).ToArrayAsync(token));
        Assert.Equal(scenario.ActorId, audit.ActorId);
        Assert.Equal(request.UserId, audit.TargetId);
        Assert.Equal(Accepted, audit.Outcome);
    }

    /// <summary>Builds independent native authority with one selected requirement missing and a valid session unchanged.</summary>
    private static async Task<GovernanceRequest> PrepareMissingPermissionAsync(GovernanceScenario scenario, string permission)
    {
        if (permission == AssignAdministrators)
        {
            _ = await GovernanceAccounts.AddAsync(scenario);
            await GrantState.SetMembershipsAsync(scenario, GrantCases.Identity);
            return await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.Assign);
        }
        var request = await GovernanceCommands.PrepareAsync(scenario, permission switch
        {
            WriteUsers => GovernanceCommands.Assign,
            RolePermissions => GovernanceCommands.RemoveGrant,
            DisableUsers => GovernanceCommands.Disable,
            _ => throw new ArgumentOutOfRangeException(nameof(permission))
        });
        await GrantState.RemoveClaimAsync(scenario, GrantCases.Platform, permission);
        return request;
    }

    /// <summary>Also isolates the current-grant boundary from independently protective survivor checks without leaking successful scopes.</summary>
    private static async Task RequireDirectBoundaryAsync(GovernanceScenario scenario, GovernanceRequest request)
    {
        GovernanceOperation? unauthorized = null;
        try
        {
            try
            {
                unauthorized = await GovernanceOperation.OpenAsync(scenario, request);
            }
            catch (FactorUnavailableException) { }
            Assert.Null(unauthorized);
        }
        finally
        {
            if (unauthorized is not null)
            {
                await unauthorized.DisposeAsync();
            }
        }
    }
    public static IEnumerable<TheoryDataRow<string, string>> Sessions => GovernanceRows.For(
        GovernanceSessionDamage.Missing, GovernanceSessionDamage.Revoked, GovernanceSessionDamage.DifferentUser, GovernanceSessionDamage.AuthenticationGeneration, GovernanceSessionDamage.FactorGeneration,
        GovernanceSessionDamage.SecurityStamp, GovernanceSessionDamage.Methods, GovernanceSessionDamage.Idle, GovernanceSessionDamage.Absolute, GovernanceSessionDamage.Recovery);

    /// <summary>Invalid stored bindings or restricted recovery cannot authorize a governed write or consume accepted proof.</summary>
    [Theory, MemberData(nameof(Sessions))]
    public async Task Given_ConfiguredAdministrator_When_CurrentSessionBindingIsInvalid_Then_CommandDenies(string provider, string variant)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RestoreGrant);
        await GovernanceSessionEvidence.RequireProofAsync(scenario, request, scenario.Session, true);
        if (variant == GovernanceSessionDamage.Methods)
        {
            await GovernanceSessionEvidence.RequireMethodsConstraintAsync(scenario, request, provider);
            return; // Real schema rejection plus detached predicate control; no impossible persisted session is claimed.
        }
        var candidate = await GovernanceSessionDamage.ApplyAsync(scenario, variant);
        var before = await scenario.SnapshotAsync();
        await GovernanceSessionEvidence.RequireProofAsync(scenario, request, candidate, false);
        Assert.Equal(GovernanceOutcome.Denied, await scenario.ExecuteAsync(request, session: candidate));
        Assert.Equal(before, await scenario.SnapshotAsync());
    }
    private const string NegativeNativeFailures = "negative-native-failures";
    private const string StaleFactorAllocator = "stale-factor-allocator";
    private const int InvalidFailureCount = -1;
    public static IEnumerable<TheoryDataRow<string, string>> IncoherentActors =>
        GovernanceRows.For(NegativeNativeFailures, StaleFactorAllocator);

    /// <summary>Current read and command authority share credential coherence even when a separate valid platform survivor exists.</summary>
    [Theory, MemberData(nameof(IncoherentActors))]
    public async Task Given_IncoherentActorAndHealthyWitness_When_GovernanceIsRequested_Then_NoProofOrAuthorityChangeCommits(string provider, string condition)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var witness = await GovernanceAccounts.AddAsync(scenario);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.Assign);
        await DamageActorAsync(scenario, condition);
        var before = await scenario.SnapshotAsync();
        var observed = await scenario.ReadAsync();
        Assert.Empty(observed.Permissions);
        Assert.Equal(witness, Assert.Single(observed.UsableAdministrators));
        Assert.Equal(GovernanceOutcome.Denied, await scenario.ExecuteAsync(request));
        var code = await scenario.UnusedCodeAsync();
        await using (var operation = await GovernanceOperation.OpenAsync(scenario, request))
        {
            await Assert.ThrowsAsync<FactorUnavailableException>(() => operation.VerifyAsync(scenario, code));
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Persists one schema-permitted incoherence without changing the otherwise current full session or password.</summary>
    private static async Task DamageActorAsync(GovernanceScenario scenario, string condition)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        if (condition == NegativeNativeFailures)
        {
            var user = await database.Users.SingleAsync(row => row.Id == scenario.ActorId, token);
            user.AccessFailedCount = InvalidFailureCount;
        }
        else
        {
            Assert.Equal(StaleFactorAllocator, condition);
            var authentication = await database.Set<AdministratorAuthentication>().SingleAsync(row => row.UserId == scenario.ActorId, token);
            authentication.LastAllocatedFactorGeneration = 0;
        }
        await database.SaveChangesAsync(token);
    }
}
