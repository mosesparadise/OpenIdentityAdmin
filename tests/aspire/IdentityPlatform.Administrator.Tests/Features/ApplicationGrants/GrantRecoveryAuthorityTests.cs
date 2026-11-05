using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Separates immutable original approval from the genuinely authenticated replacement approver's current batch authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantRecoveryAuthorityTests(BootstrapFixture fixture)
{
    private const int MultipleBatches = 17;
    private const int TwoBatches = 16;
    private const string Reason = "security";
    private const string GuidFormat = "D";

    /// <summary>A second native password/MFA session can continue stopped work while the original actor stays disabled; removing the new actor's roles stops the next batch.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_DisabledOriginalApprover_When_AnotherAdministratorContinues_Then_ImmutableHistoryAndEffectiveAuthorityRemainDistinct(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, MultipleBatches, MultipleBatches);
        var accepted = await test.AdmitAsync(test.Intent());
        Assert.True(await test.BatchAsync(accepted.Id));
        await DisableOriginalAsync(test);
        Assert.True(await test.BatchAsync(accepted.Id));
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        Assert.Equal(GrantJobState.Stopped, before.Job.State);
        Assert.Equal(GrantJobFailure.Authority, before.Job.Failure);
        var account = test.Fixture.Scenario.Account;
        var actor = await GovernanceAccounts.AddAsync(account);
        test.Fixture.Clock.Now = DateTimeOffset.UtcNow;
        var session = await GovernanceAccounts.LoginAsync(account, actor);
        var identity = await test.Fixture.NativeIdentityAsync(actor, session.SessionId);
        var intent = GrantIntent.Create(test.Id, test.Application.Version!, Guid.NewGuid().ToString(GuidFormat),
            new(Reason, true, ApplicationVersion.Create(accepted.Id, before.Job.Revision)), before.Job.Id)!;
        await test.AdmitOnAsync(test.Fixture.Api, intent, identity);
        Assert.True(await test.BatchAsync(accepted.Id));
        var advanced = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        advanced.Require();
        advanced.Preserves(before);
        Assert.Equal(TwoBatches, advanced.Job.TokenCount);
        Assert.Equal(actor, Assert.Single(advanced.Approvals, row => row.Id == advanced.Job.EffectiveApprovalId).ActorId);
        Assert.Equal(2, advanced.Approvals.Length);
        await RemoveNewAuthorityAsync(test, actor);
        var native = await test.NativeSnapshotAsync();
        Assert.True(await test.BatchAsync(accepted.Id));
        var stopped = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        stopped.Require();
        stopped.Preserves(advanced);
        Assert.Equal(GrantJobState.Stopped, stopped.Job.State);
        Assert.Equal(GrantJobFailure.Authority, stopped.Job.Failure);
        Assert.Equal(TwoBatches, stopped.Job.TokenCount);
        Assert.Equal(native, await test.NativeSnapshotAsync());
    }

    /// <summary>Changes the original persisted account independently of the accepted job and its audit history.</summary>
    private static async Task DisableOriginalAsync(GrantTestContext test)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Users.Where(row => row.Id == test.Fixture.Identity.Subject)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, false), test.Token));
    }

    /// <summary>Removes only the effective approver's persisted memberships and confirms the original account was never silently restored.</summary>
    private static async Task RemoveNewAuthorityAsync(GrantTestContext test, string actor)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await database.Users.Where(row => row.Id == test.Fixture.Identity.Subject).Select(row => row.IsEnabled).SingleAsync(test.Token));
        Assert.True(await database.UserRoles.Where(row => row.UserId == actor).ExecuteDeleteAsync(test.Token) > 0);
    }
}
