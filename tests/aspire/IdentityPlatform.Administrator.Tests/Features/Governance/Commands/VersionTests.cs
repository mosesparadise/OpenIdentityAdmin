using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Commands;

/// <summary>Checks committed internal authority transitions independently of manager return values.</summary>
[Collection("administrator-bootstrap")]
public sealed class VersionTests(BootstrapFixture fixture)
{
    private const string SpecialistRole = "IdentityAdministrator";
    private const string PermissionType = "identityplatform.permission";
    private const string UserRead = "users.read";
    private const string GrantedEvent = "AdministratorGrantAdded";
    private const string Accepted = "Accepted";
    private const string CriticalDecision = "CriticalDecision";
    private const string VersionDecision = "governance-version";

    /// <summary>A native grant transition commits exactly one semantic version, protected reservation and actor/role audit.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests)), Trait(CriticalDecision, VersionDecision)]
    public async Task Given_StableAuthorityVersion_When_ActivityAndGovernedChangesOccur_Then_OnlyCommittedAuthorityChangesAdvanceIt(string provider)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var token = TestContext.Current.CancellationToken;
        var roleId = await RemoveSpecialistGrantAsync(scenario);
        await using (var activity = scenario.CreateScope())
        {
            Assert.NotNull(await activity.ServiceProvider.GetRequiredService<JourneySessions>()
                .ValidateAsync(scenario.ActorId, scenario.Grant.SessionId, token));
            var observation = await activity.ServiceProvider.GetRequiredService<GovernanceService>().ReadAsync(scenario.ActorId, token);
            Assert.Equal(0, observation.AuthorityRevision);
        }
        var request = await scenario.RequestAsync(GovernanceAction.AddGrant, roleId: roleId, permission: UserRead);
        await GovernanceVersionWriteEvidence.RequireAsync(scenario, request);
        var code = await scenario.UnusedCodeAsync();
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using (var command = scenario.CreateScope())
        {
            var service = command.ServiceProvider.GetRequiredService<GovernanceService>();
            Assert.Equal(GovernanceOutcome.Accepted, await service.ExecuteAsync(request, scenario.Session, scenario.Password, code, token));
        }
        FactorOracle.RequireWindow(scenario.Journey.Seed, code, started, true);
        await using var verify = scenario.CreateScope();
        var current = verify.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await current.Set<AdministratorGovernance>().Select(row => row.AuthorityRevision).SingleAsync(token));
        Assert.Single(await current.RoleClaims.Where(row => row.RoleId == roleId && row.ClaimType == PermissionType && row.ClaimValue == UserRead).ToArrayAsync(token));
        var audit = Assert.Single(await current.Set<SecurityAuditIntent>().Where(row => row.Event == GrantedEvent).ToArrayAsync(token));
        Assert.Equal(scenario.ActorId, audit.ActorId);
        Assert.Equal(roleId, audit.TargetId);
        Assert.Equal(Accepted, audit.Outcome);
        Assert.NotEqual(Guid.Empty, audit.CorrelationId);
        Assert.Equal(2, await current.Set<AdministratorReplay>().CountAsync(token));
        await RequireStaleIntentRollbackAsync(scenario, request, code);
    }

    /// <summary>Removes one fixture specialist grant without changing the actor's complete platform bundle.
    /// This prepares independent restore intent; it is not a product management operation.</summary>
    private static async Task<string> RemoveSpecialistGrantAsync(GovernanceScenario scenario)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var roleId = await database.Roles.Where(role => role.Name == SpecialistRole).Select(role => role.Id).SingleAsync(token);
        Assert.Equal(1, await database.RoleClaims.Where(row => row.RoleId == roleId && row.ClaimType == PermissionType && row.ClaimValue == UserRead).ExecuteDeleteAsync(token));
        return roleId;
    }

    /// <summary>Reusing stale intent cannot advance a version or consume another proof, even after an earlier command committed.</summary>
    private static async Task RequireStaleIntentRollbackAsync(GovernanceScenario scenario, GovernanceRequest request, string code)
    {
        var before = await scenario.SnapshotAsync();
        await using var scope = scenario.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<GovernanceService>();
        await Assert.ThrowsAsync<GovernanceUnavailableException>(() => service.ExecuteAsync(request, scenario.Session,
            scenario.Password, code, TestContext.Current.CancellationToken));
        Assert.Equal(before, await scenario.SnapshotAsync());
    }
    public static IEnumerable<TheoryDataRow<string, string>> StaleVersions => GovernanceRows.For(
        GovernanceVersionDamage.Authority, GovernanceVersionDamage.UserStamp, GovernanceVersionDamage.RoleStamp, GovernanceVersionDamage.Enrollment);

    /// <summary>One independently changed expected version rejects original intent before proof, audit or authority writes.</summary>
    [Theory, MemberData(nameof(StaleVersions))]
    public async Task Given_OriginalIntent_When_ExpectedVersionChanges_Then_CommandDeniesBeforeWriting(string provider, string variant)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.Assign);
        await GovernanceVersionDamage.RequireCurrentAsync(scenario, request);
        await GovernanceVersionDamage.ChangeAsync(scenario, request, variant);
        var before = await scenario.SnapshotAsync();
        await Assert.ThrowsAsync<GovernanceUnavailableException>(() => scenario.ExecuteAsync(request));
        Assert.Equal(before, await scenario.SnapshotAsync());
        var fresh = await scenario.RequestAsync(request.Action, request.UserId, request.RoleId, request.Permission);
        GovernanceVersionDamage.RequireOnlySelectedVersionChanged(request, fresh, variant);
        await GovernanceVersionDamage.RequireCurrentAsync(scenario, fresh);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }
}
