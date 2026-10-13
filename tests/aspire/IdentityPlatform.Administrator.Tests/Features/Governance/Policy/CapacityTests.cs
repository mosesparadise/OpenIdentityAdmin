using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Policy;

/// <summary>Proves actual native command success at each independent bound and exact rollback on overflow.</summary>
[Collection("administrator-bootstrap")]
public sealed class CapacityTests(BootstrapFixture fixture)
{
    private const string AssignedEvent = "AdministratorBundleAssigned";
    private const int ExpectedActorReplayEntries = 2;
    private const string Accepted = "Accepted";
    public static IEnumerable<TheoryDataRow<string, string>> Cases => GovernanceRows.For(CapacityCases.All.Select(row => row.Name).ToArray());

    /// <summary>Uses full discovery, native proof and committed effects; overflow may return neither a truncated decision nor partial writes.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_BoundedAuthorityRecords_When_ReadAtLimitOrLimitPlusOne_Then_OnlyCompleteBoundedDecisionsSucceed(string provider, string condition)
    {
        var test = CapacityCases.All.Single(row => row.Name == condition);
        var reads = new CapacitySqlEvidence();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(reads)));
        var state = await CapacityState.CreateAsync(scenario, test);
        var before = await scenario.SnapshotAsync();
        reads.Begin(test, state.AccountRoles);
        if (test.Overflows)
        {
            await Assert.ThrowsAsync<GovernanceUnavailableException>(() => scenario.ExecuteAsync(state.Request));
            reads.Stop();
            reads.RequireBoundedRetrieval();
            Assert.Equal(before, await scenario.SnapshotAsync());
            return;
        }
        Assert.Equal(GovernanceOutcome.Accepted, await scenario.ExecuteAsync(state.Request));
        reads.Stop();
        reads.RequireBoundedRetrieval();
        Assert.Equal(state.Accounts, reads.AccountLocks.Order(StringComparer.Ordinal));
        await RequireCommittedAsync(scenario, state);
    }

    /// <summary>Requires native membership, one semantic revision, exact actor/target audit and complete independent survivor/grant sets.</summary>
    private static async Task RequireCommittedAsync(GovernanceScenario scenario, CapacityState state)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        Assert.True(await database.UserRoles.AnyAsync(row => row.UserId == state.Request.UserId && row.RoleId == state.Request.RoleId, token));
        Assert.Equal(state.Request.Versions.AuthorityRevision + 1,
            await database.Set<AdministratorGovernance>().Select(row => row.AuthorityRevision).SingleAsync(token));
        var audit = Assert.Single(await database.Set<SecurityAuditIntent>().Where(row => row.Event == AssignedEvent).ToArrayAsync(token));
        Assert.Equal(scenario.ActorId, audit.ActorId);
        Assert.Equal(state.Request.UserId, audit.TargetId);
        Assert.Equal(Accepted, audit.Outcome);
        var observation = await scenario.ReadAsync();
        Assert.Equal(new[] { scenario.ActorId, state.Witness }.Order(StringComparer.Ordinal),
            observation.UsableAdministrators.Order(StringComparer.Ordinal));
        GrantState.RequirePermissions(GrantCases.Expected(GrantCases.Platform), observation);
        var target = await database.Users.AsNoTracking().SingleAsync(row => row.Id == state.Request.UserId, token);
        Assert.False(target.IsEnabled);
        Assert.False(target.TwoFactorEnabled);
        Assert.Null(target.PasswordHash);
        Assert.Equal(ExpectedActorReplayEntries, await database.Set<AdministratorReplay>().CountAsync(row => row.UserId == scenario.ActorId, token));
    }
}
