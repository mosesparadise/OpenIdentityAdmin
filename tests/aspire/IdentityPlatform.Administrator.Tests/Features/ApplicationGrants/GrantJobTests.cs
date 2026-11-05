using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Verifies bounded committed native progress, durable authority and explicit recovery on each real relational engine.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantJobTests(BootstrapFixture fixture)
{
    private const string PeerName = "unaffected-grant-peer";
    private const int Conflict = 409;
    private const string EnableBypass = "grant-enable-conflict-bypass";
    private const string PermissionBypass = "grant-current-permission-bypass";
    private const string GuidFormat = "D";
    private const string PermissionClaim = "identityplatform.permission";

    private const int MultiBatchCount = 9;
    private const int BeyondReplaySeconds = 86401;
    private const string Valid = "valid";
    private const string Revoked = "revoked";
    private const string Permission = "grants.revoke";
    private const string Reason = "security";

    /// <summary>Revokes both native families in finite batches, keeps peers intact and prevents re-enable until successful reconciliation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql", "CriticalDecision", "grant-enable-conflict"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer", "CriticalDecision", "grant-enable-conflict"])]
    public async Task Given_DisabledApplicationWithNineGrants_When_Batched_Then_ExactNativeProgressPrecedesSeparateReenable(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        var peer = await test.Fixture.ExecuteAsync(test.Fixture.Create(PeerName));
        Assert.Equal(201, peer.Status);
        await test.SeedAsync(test.Id, MultiBatchCount, MultiBatchCount);
        await test.SeedAsync(peer.Application!.Id, 1, 1);
        var accepted = await test.AdmitAsync(test.Intent());
        Assert.Equal("queued", accepted.State);
        Assert.Equal("0", accepted.TokenCount);
        var initialHistory = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        initialHistory.Require();
        var enable = ApplicationMutationCommand.Lifecycle(test.Id, test.Application.Version!, new(Reason, true), true)!;
        Assert.True((await test.Fixture.ExecuteAsync(enable)).Status == Conflict, EnableBypass);
        Assert.True(await test.BatchAsync(accepted.Id));
        var first = await test.ReadAsync(accepted.Id);
        Assert.Equal(8, first.AuthorizationCount);
        Assert.Equal(8, first.TokenCount);
        Assert.Equal(GrantJobState.Processing, first.State);
        Assert.Equal(409, (await test.Fixture.ExecuteAsync(enable)).Status);
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(await test.BatchAsync(accepted.Id));
        var complete = await test.ReadAsync(accepted.Id);
        Assert.Equal(GrantJobState.Succeeded, complete.State);
        Assert.Equal(9, complete.AuthorizationCount);
        Assert.Equal(9, complete.TokenCount);
        var native = await test.StatusesAsync(test.Id);
        Assert.All(native.Authorizations, status => Assert.Equal(Revoked, status));
        Assert.All(native.Tokens, status => Assert.Equal(Revoked, status));
        var other = await test.StatusesAsync(peer.Application.Id);
        Assert.Equal(Valid, Assert.Single(other.Authorizations));
        Assert.Equal(Valid, Assert.Single(other.Tokens));
        Assert.Equal(200, (await test.Fixture.ExecuteAsync(enable)).Status);
        Assert.False(await test.BatchAsync(accepted.Id));
        var history = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        history.Require();
        history.Preserves(initialHistory);
    }

    /// <summary>Sign-out, old MFA and the idempotency replay deadline never become worker authority deadlines; expired target credentials remain cleanable.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_AcceptedJob_When_SessionRevokedAndMoreThanOneDayPasses_Then_CurrentAccountCanFinishWithoutFreshMfa(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, 1, 1);
        var accepted = await test.AdmitAsync(test.Intent());
        await using (var scope = test.Fixture.Scope())
        {
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AdministratorSession>()
                .Where(row => row.PublicId == test.Fixture.Identity.Session).ExecuteUpdateAsync(update => update.SetProperty(row => row.Revoked, true), test.Token));
        }
        test.Fixture.Clock.Now = test.Fixture.Clock.Now.AddSeconds(BeyondReplaySeconds);
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.Equal(GrantJobState.Succeeded, (await test.ReadAsync(accepted.Id)).State);
        Assert.Equal(Revoked, Assert.Single((await test.StatusesAsync(test.Id)).Tokens));
    }

    /// <summary>Current permission loss stops remaining work; restoring permission cannot revive it until a fresh explicit continuation is committed.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql", "CriticalDecision", "grant-current-permission"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer", "CriticalDecision", "grant-current-permission"])]
    public async Task Given_PartialJob_When_PermissionIsRemovedAndRestored_Then_OnlyExplicitContinuationResumes(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, MultiBatchCount, MultiBatchCount);
        var accepted = await test.AdmitAsync(test.Intent());
        Assert.True(await test.BatchAsync(accepted.Id));
        var priorHistory = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        var roleIds = await RemovePermissionAsync(test);
        Assert.True(await test.BatchAsync(accepted.Id));
        var stopped = await test.ReadAsync(accepted.Id);
        Assert.True(stopped.State == GrantJobState.Stopped, PermissionBypass);
        Assert.Equal(GrantJobFailure.Authority, stopped.Failure);
        Assert.Equal(8, stopped.TokenCount);
        await RestorePermissionAsync(test, roleIds);
        Assert.False(await test.BatchAsync(accepted.Id));
        var recovery = GrantIntent.Create(test.Id, test.Application.Version!, Guid.NewGuid().ToString(GuidFormat),
            new(Reason, true, ApplicationVersion.Create(stopped.Id.ToString(GuidFormat), stopped.Revision)), stopped.Id)!;
        var resumed = await test.AdmitAsync(recovery);
        Assert.Equal(accepted.Id, resumed.Id);
        Assert.Equal("8", resumed.TokenCount);
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.Equal(GrantJobState.Succeeded, (await test.ReadAsync(accepted.Id)).State);
        var history = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        history.Require();
        history.Preserves(priorHistory);
        Assert.Equal(2, history.Approvals.Length);
    }

    /// <summary>Removes actual stored claims without role-default fallback in an isolated synthetic database.</summary>
    private static async Task<string[]> RemovePermissionAsync(GrantTestContext test)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var roles = await database.RoleClaims.Where(row => row.ClaimValue == Permission).Select(row => row.RoleId).ToArrayAsync(test.Token);
        Assert.NotEmpty(roles);
        Assert.Equal(roles.Length, await database.RoleClaims.Where(row => row.ClaimValue == Permission).ExecuteDeleteAsync(test.Token));
        return roles;
    }

    /// <summary>Restores the same finite permission claims, preserving normal native generated identifiers.</summary>
    private static async Task RestorePermissionAsync(GrantTestContext test, string[] roles)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        foreach (var role in roles)
        {
            database.RoleClaims.Add(new()
            {
                RoleId = role,
                ClaimType = PermissionClaim,
                ClaimValue = Permission
            });
        }
        await database.SaveChangesAsync(test.Token);
    }
}
