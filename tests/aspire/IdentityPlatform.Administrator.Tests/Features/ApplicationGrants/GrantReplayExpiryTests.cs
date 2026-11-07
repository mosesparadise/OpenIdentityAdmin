using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Core.Features.ApplicationGrants;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Distinguishes the retained idempotency deadline from expired session authority using a real new password/MFA ceremony.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantReplayExpiryTests(BootstrapFixture fixture)
{
    private const int Unauthorized = 401;
    private const int Conflict = 409;
    private static readonly TimeSpan ReplayWindow = TimeSpan.FromHours(24);

    /// <summary>Fresh current authority cannot replay a retained key at its original deadline, while a new explicit key can create a separate cleanup job.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RetainedKeyAtDeadline_When_SameActorAuthenticatesAgain_Then_OldIntentConflictsAndFreshIntentCanBeAccepted(string provider)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        var intent = test.Intent();
        var accepted = await test.AdmitAsync(intent);
        Assert.True(await test.BatchAsync(accepted.Id));
        var before = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        Assert.Equal(GrantJobState.Succeeded, before.Job.State);
        test.Fixture.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(before.Approvals.Single().ApprovedAt) + ReplayWindow;
        var expired = await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.AdmitAsync(intent));
        Assert.Equal(Unauthorized, expired.Status);
        var account = test.Fixture.Scenario.Account;
        var session = await GovernanceAccounts.LoginAsync(account.CreateScope, account.Password, account.ActorId, await account.UnusedCodeAsync());
        var identity = await test.Fixture.NativeIdentityAsync(account.ActorId, session.SessionId);
        Assert.NotEqual(test.Fixture.Identity.Session, identity.Session);
        var replay = await Assert.ThrowsAsync<ApplicationHttpRejected>(() => test.AdmitOnAsync(test.Fixture.Api, intent, identity));
        Assert.Equal(Conflict, replay.Status);
        var after = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        after.Require();
        after.Preserves(before);
        Assert.Equal(before.Job.Revision, after.Job.Revision);
        Assert.Single(after.Approvals);
        var replacement = await test.AdmitOnAsync(test.Fixture.Api, test.Intent(), identity);
        Assert.NotEqual(accepted.Id, replacement.Id);
        Assert.Equal("queued", replacement.State);
    }
}
