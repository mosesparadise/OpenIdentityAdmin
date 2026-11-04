using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves prepared-claim contamination cannot be legitimized by native signing or the final output snapshot.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedTokenClaimTests(BootstrapFixture fixture)
{
    private const string ClaimBypass = "managed-claim-isolation-bypass";

    private const string CodeTarget = "code";
    private const string Redeemed = "redeemed";
    /// <summary>Each actual prepared token is independently poisoned after native cloning; no token or partial issuance row escapes.</summary>
    [Theory]
    [InlineData("PostgreSql", CodeTarget, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", CodeTarget, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", "access", Traits = ["Provider", "PostgreSql", "CriticalDecision", "managed-claim-isolation"])]
    [InlineData("SqlServer", "access", Traits = ["Provider", "SqlServer", "CriticalDecision", "managed-claim-isolation"])]
    [InlineData("PostgreSql", "identity", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", "identity", Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", "nonce", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", "nonce", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealPreparedManagedToken_When_AdministratorClaimIsInjectedBeforeSigning_Then_IssuanceRollsBackWithoutAuthority(string provider, string target)
    {
        var fault = new ManagedPreparedMutation(target);
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = target == CodeTarget ? null : await flow.CodeAsync();
        var before = await test.SnapshotAsync();
        var authority = await ManagedHumanEvidence.AuthorityAsync(test);
        using var response = code is null ? await test.SendAsync(flow.Resume) : await flow.ExchangeAsync(code);
        Assert.True(fault.Fired);
        Assert.False(response.StatusCode == System.Net.HttpStatusCode.OK, ClaimBypass);
        await ManagedCacheTestContext.UnavailableAsync(response);
        var after = await test.SnapshotAsync();
        Assert.Equal(before.Tokens.Select(row => row.Id), after.Tokens.Select(row => row.Id));
        Assert.Equal(before.Authorizations, after.Authorizations);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(test));
        var row = Assert.Single(after.Interactions);
        Assert.True(after.Tokens.Single(value => value.Id == (code is null ? row.RequestId : row.CodeId)).Status == Redeemed);
    }
}
