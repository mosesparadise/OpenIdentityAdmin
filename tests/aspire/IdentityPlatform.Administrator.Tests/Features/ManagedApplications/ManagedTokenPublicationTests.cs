using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Separates committed managed issuance from response publication after actual provider acknowledgement and context cleanup.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedTokenPublicationTests(BootstrapFixture fixture)
{
    private const int CodeDelta = 1;
    private const int ExchangeDelta = 2;

    /// <summary>Original human expiry after actual issuance commit/disposal prevents credential publication without pretending committed rows rolled back.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CommittedManagedIssuance_When_OriginalDeadlineExpiresAfterCleanup_Then_RowsRemainButCredentialsAreWithheld(string provider, bool exchange)
    {
        var fault = new ManagedCacheCompletionFault(cleanup: false, issuance: true) { Armed = false };
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = exchange ? await flow.CodeAsync() : null;
        var before = await test.SnapshotAsync();
        var expiry = Assert.Single(before.Interactions).ExpiresAt;
        fault.Advance = () => test.Http.Environment.Issuer.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(expiry);
        fault.Armed = true;
        using var response = exchange ? await flow.ExchangeAsync(code!) : await test.SendAsync(flow.Resume);
        await ManagedCacheTestContext.UnavailableAsync(response);
        Assert.Equal(1, fault.Commits);
        Assert.Equal(1, fault.Disposals);
        Assert.Equal(1, fault.Faults);
        var after = await test.SnapshotAsync();
        Assert.Equal(before.Tokens.Length + (exchange ? ExchangeDelta : CodeDelta), after.Tokens.Length);
        Assert.Equal(expiry, Assert.Single(after.Interactions).ExpiresAt);
    }

    /// <summary>A real native response callback cannot add a substituted state value after output preparation and survive final commit checks.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ManagedNativeResponse_When_ActualCallbackChangesOutput_Then_IssuanceRollsBackAndResponseIsWithheld(string provider, bool exchange)
    {
        var fault = new ManagedIssuanceCallback();
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = exchange ? await flow.CodeAsync() : null;
        var before = await ManagedTokenEvidence.RollbackDigestAsync(test);
        fault.Armed = true;
        using var response = exchange ? await flow.ExchangeAsync(code!) : await test.SendAsync(flow.Resume);
        await ManagedCacheTestContext.UnavailableAsync(response);
        Assert.True(fault.Fired);
        Assert.Equal(before, await ManagedTokenEvidence.RollbackDigestAsync(test));
    }

    /// <summary>Actual committed rows remain when acknowledgement or disposal fails, but no native credential response is published.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CommittedManagedIssuance_When_AcknowledgementOrDisposalFails_Then_DurableRowsRemainWithoutPublishedCredentials(string provider, bool exchange, bool cleanup)
    {
        var fault = new ManagedCacheCompletionFault(cleanup, issuance: true) { Armed = false };
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = exchange ? await flow.CodeAsync() : null;
        var before = await test.SnapshotAsync();
        var authority = await ManagedHumanEvidence.AuthorityAsync(test);
        fault.Armed = true;
        using var response = exchange ? await flow.ExchangeAsync(code!) : await test.SendAsync(flow.Resume);
        await ManagedCacheTestContext.UnavailableAsync(response);
        Assert.Equal(1, fault.Commits);
        Assert.Equal(1, fault.Disposals);
        Assert.Equal(1, fault.Faults);
        var after = await test.SnapshotAsync();
        Assert.Equal(before.Tokens.Length + (exchange ? ExchangeDelta : CodeDelta), after.Tokens.Length);
        Assert.Equal(ManagedLoginState.Consumed, Assert.Single(after.Interactions).State);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(test));
    }
}
