using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Policy;

/// <summary>Requires one coherent current-authority snapshot while an actual governed writer contends for its serialized boundary.</summary>
[Collection("administrator-bootstrap")]
public sealed class ConsistencyTests(BootstrapFixture fixture)
{
    private const string RemovedAudit = "AdministratorGrantRemoved";
    public static IEnumerable<TheoryDataRow<string, string>> Boundaries => GovernanceRows.For(
        GovernanceReadGate.AfterMembership, GovernanceReadGate.BeforeClaims);

    /// <summary>The full reader returns its pre-write permissions, then a committed writer changes the next complete observation.</summary>
    [Theory, MemberData(nameof(Boundaries))]
    public async Task Given_SerializedCurrentAuthority_When_AnotherWriterContendsBetweenReads_Then_NoMixedSnapshotEscapes(string provider, string boundary)
    {
        var gate = new GovernanceReadGate();
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(gate)));
        var writerId = await GovernanceAccounts.AddAsync(scenario);
        var writer = await GovernanceAccounts.LoginAsync(scenario, writerId);
        await GrantState.SetMembershipsAsync(scenario, GrantCases.Identity);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RemoveGrant);
        var before = await scenario.ReadAsync();
        GrantState.RequirePermissions(GrantCases.Expected(GrantCases.Identity), before);
        var code = await scenario.UnusedCodeAsync(writerId, FactorScenario.Seed);
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var observed = await GovernanceReadContention.RunAsync(scenario, gate, boundary, request, writer,
            GovernanceAccounts.Password(scenario), code);
        FactorOracle.RequireWindow(FactorScenario.Seed, code, started, true);
        GrantState.RequirePermissions(GrantCases.Expected(GrantCases.Identity), observed);
        Assert.Equal(before.AuthorityRevision, observed.AuthorityRevision);
        Assert.Equal(writerId, Assert.Single(observed.UsableAdministrators));
        var after = await scenario.ReadAsync();
        GrantState.RequirePermissions(GrantCases.Expected(GrantCases.Identity).Where(value => value != GrantCases.UserRead), after);
        Assert.Equal(before.AuthorityRevision + 1, after.AuthorityRevision);
        await RequireCommittedRemovalAsync(scenario, writerId, request.RoleId!);
    }

    /// <summary>Corroborates the accepted command using independently queried native grant absence and exact actor/target audit evidence.</summary>
    private static async Task RequireCommittedRemovalAsync(GovernanceScenario scenario, string writerId, string roleId)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        Assert.False(await database.RoleClaims.AnyAsync(row => row.RoleId == roleId && row.ClaimType == GrantCases.ClaimType &&
            row.ClaimValue == GrantCases.UserRead, token));
        var audit = Assert.Single(await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => row.Event == RemovedAudit).ToArrayAsync(token));
        Assert.Equal(writerId, audit.ActorId);
        Assert.Equal(roleId, audit.TargetId);
    }
}
