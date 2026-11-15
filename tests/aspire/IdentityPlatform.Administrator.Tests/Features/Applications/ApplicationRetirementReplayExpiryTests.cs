using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.ApplicationGrants;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Distinguishes the retained idempotency deadline from expired session authority using a real new password/MFA ceremony.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationRetirementReplayExpiryTests(BootstrapFixture fixture)
{
    private const int Unauthorized = 401;
    private const int Conflict = 409;
    private static readonly TimeSpan ReplayWindow = TimeSpan.FromHours(24);

    /// <summary>Fresh current authority cannot replay a retained key at its original deadline, and a fresh key cannot create another permanent-retirement admission.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RetirementKeyAtDeadline_When_SameActorAuthenticatesAgain_Then_OldAndNewIntentsCannotRenewTerminalAdmission(string provider)
    {
        await using var test = await ApplicationRetirementTestContext.CreateAsync(fixture, provider);
        var intent = test.Intent();
        var accepted = await test.RetireAsync(intent);
        Assert.True(await test.Grants.BatchAsync(accepted.Job.Id));
        var before = await GrantAuditWitness.ReadAsync(test.Grants, accepted.Job.Id);
        Assert.Equal(GrantJobState.Succeeded, before.Job.State);
        test.Fixture.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(before.Approvals.Single().ApprovedAt) + ReplayWindow;
        var expired = await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.RetireAsync(intent));
        Assert.Equal(Unauthorized, expired.Status);
        var account = test.Fixture.Scenario.Account;
        var session = await GovernanceAccounts.LoginAsync(account.CreateScope, account.Password, account.ActorId, await account.UnusedCodeAsync());
        var identity = await test.Fixture.NativeIdentityAsync(account.ActorId, session.SessionId);
        Assert.NotEqual(test.Fixture.Identity.Session, identity.Session);
        var replay = await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.RetireAsync(intent, identity: identity));
        Assert.Equal(Conflict, replay.Status);
        var after = await GrantAuditWitness.ReadAsync(test.Grants, accepted.Job.Id);
        after.Require();
        after.Preserves(before);
        Assert.Equal(before.Job.Revision, after.Job.Revision);
        Assert.Single(after.Approvals);
        var replacement = await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.RetireAsync(test.Intent(), identity: identity));
        Assert.Equal(Conflict, replacement.Status);
        Assert.True((await ApplicationUpdateSnapshot.ReadAsync(test.Fixture, test.Id)).Policy.Retired);
        Assert.Equal(Guid.Parse(accepted.Job.Id), (await test.ReceiptAsync()).GrantJobId);
    }
}
