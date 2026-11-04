using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Distinguishes rejected client proofs from irreversible consumption followed by actual issuance rollback.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedTokenFailureTests(BootstrapFixture fixture)
{
    private const string CodeConsumptionBypass = "managed-code-consumption-bypass";
    private const string RequestConsumptionBypass = "managed-request-consumption-bypass";

    private const string InvalidClient = "invalid_client";
    private const string InvalidGrant = "invalid_grant";
    private const string InvalidToken = "invalid_token";
    private const string ForeignCallback = "https://foreign.example.test/callback";
    private const string Valid = "valid";
    private const string Redeemed = "redeemed";
    private const string CodeEvent = "managed-code-issued";
    private const string TokenEvent = "managed-token-issued";

    /// <summary>Real provider audit INSERT denial rolls back generated credentials while preserving earlier committed native input consumption.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql", "CriticalDecision", "managed-request-consumption"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer", "CriticalDecision", "managed-request-consumption"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql", "CriticalDecision", "managed-code-consumption"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer", "CriticalDecision", "managed-code-consumption"])]
    public async Task Given_RealManagedIssuance_When_ProviderAuditPermissionIsDenied_Then_CredentialsRollBackAndOriginalInputStaysSpent(string provider, bool exchange)
    {
        var witness = new ManagedRedemptionWitness();
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, witness.Configure);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = exchange ? await flow.CodeAsync() : null;
        var row = Assert.Single((await test.SnapshotAsync()).Interactions);
        witness.Identifier = (exchange ? row.CodeId : row.RequestId)!;
        witness.ReadStatus = () => ReadStatusAsync(test, witness.Identifier);
        var before = await ManagedTokenEvidence.RollbackDigestAsync(test);
        await RequireEarlyFailureAsync(test, flow, code, witness);
        Assert.Equal(before, await ManagedTokenEvidence.RollbackDigestAsync(test));
        var lease = test.Http.Environment.Issuer.Account.Journey.Lease;
        await IssuerAuditPermission.RunAsync(lease, async () =>
        {
            using var response = exchange ? await flow.ExchangeAsync(code!) : await test.SendAsync(flow.Resume);
            await ManagedCacheTestContext.UnavailableAsync(response);
        });
        witness.RequireObserved();
        var consumed = (await test.SnapshotAsync()).Tokens.Single(value => value.Id == (exchange ? row.CodeId : row.RequestId)).Status;
        Assert.False(witness.InputRemainedValid && consumed == Valid, exchange ? CodeConsumptionBypass : RequestConsumptionBypass);
        await RequireStatusAsync(test, (exchange ? row.CodeId : row.RequestId)!, Redeemed);
        Assert.Equal(before, await ManagedTokenEvidence.RollbackDigestAsync(test));
        using var replay = exchange ? await flow.ExchangeAsync(code!) : await test.SendAsync(flow.Resume);
        await ManagedTokenEvidence.ErrorAsync(replay, HttpStatusCode.BadRequest, exchange ? InvalidGrant : InvalidToken, local: !exchange);
    }

    /// <summary>A genuine early unavailable response with an unspent input cannot satisfy the commit witness or its mutation marker.</summary>
    private static async Task RequireEarlyFailureAsync(ManagedCacheTestContext test, ManagedTokenFlow flow, string? code, ManagedRedemptionWitness witness)
    {
        witness.FailBeforeRedemption = true;
        try
        {
            using var failed = code is null ? await test.SendAsync(flow.Resume) : await flow.ExchangeAsync(code);
            await ManagedCacheTestContext.UnavailableAsync(failed);
            Assert.True(witness.EarlyFailed);
            Assert.False(witness.Observed);
            Assert.Throws<InvalidOperationException>(witness.RequireObserved);
            await RequireStatusAsync(test, witness.Identifier!, Valid);
        }
        finally { witness.FailBeforeRedemption = false; }
    }

    /// <summary>Wrong native Basic, S256 or redirect proofs cannot spend the code; the original valid exchange still succeeds afterward.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RealManagedCode_When_ClientSecretVerifierOrCallbackIsWrong_Then_CodeRemainsUsableOnlyWithOriginalProofs(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = await flow.CodeAsync();
        var before = await ManagedHumanEvidence.DigestAsync(test);
        using var secret = await flow.ExchangeAsync(code, secret: NativeCacheProfile.Random());
        await ManagedTokenEvidence.ErrorAsync(secret, HttpStatusCode.Unauthorized, InvalidClient);
        Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
        using var verifier = await flow.ExchangeAsync(code, proof: NativeCacheProfile.Random());
        await ManagedTokenEvidence.ErrorAsync(verifier, HttpStatusCode.BadRequest, InvalidGrant);
        Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
        using var callback = await flow.ExchangeAsync(code, callback: ForeignCallback);
        await ManagedTokenEvidence.ErrorAsync(callback, HttpStatusCode.BadRequest, InvalidGrant);
        Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
        using var accepted = await flow.ExchangeAsync(code);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    /// <summary>A separate connection observes committed redemption before the deliberately failed final save; original replay cannot resurrect it.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_DurableNativeConsumption_When_LaterIssuanceSaveFails_Then_RollbackCannotMakeOriginalInputUsable(string provider, bool exchange)
    {
        var fault = new ManagedIssuanceSaveFault();
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = exchange ? await flow.CodeAsync() : null;
        var before = await test.SnapshotAsync();
        var row = Assert.Single(before.Interactions);
        var identifier = (exchange ? row.CodeId : row.RequestId)!;
        Assert.True(before.Tokens.Single(value => value.Id == identifier).Status == Valid);
        fault.Event = exchange ? TokenEvent : CodeEvent;
        fault.Witness = () => RequireStatusAsync(test, identifier, Redeemed);
        var authority = await ManagedHumanEvidence.AuthorityAsync(test);
        var unchanged = await ManagedTokenEvidence.RollbackDigestAsync(test);
        using var failed = exchange ? await flow.ExchangeAsync(code!) : await test.SendAsync(flow.Resume);
        await ManagedCacheTestContext.UnavailableAsync(failed);
        Assert.True(fault.Fired);
        await RequireStatusAsync(test, identifier, Redeemed);
        await RequireRollbackAsync(test, before, fault.Event);
        Assert.Equal(unchanged, await ManagedTokenEvidence.RollbackDigestAsync(test));
        fault.Event = null;
        using var replay = exchange ? await flow.ExchangeAsync(code!) : await test.SendAsync(flow.Resume);
        await ManagedTokenEvidence.ErrorAsync(replay, HttpStatusCode.BadRequest, exchange ? InvalidGrant : InvalidToken, local: !exchange);
        Assert.Equal(before.Tokens.Length, (await test.SnapshotAsync()).Tokens.Length);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(test));
    }

    /// <summary>Uses an independent provider connection while the failing issuance transaction is still active.</summary>
    private static async Task RequireStatusAsync(ManagedCacheTestContext test, string identifier, string expected)
        => Assert.True(await ReadStatusAsync(test, identifier) == expected);

    /// <summary>Reads one exact original input on a fresh independent connection without printing its identifier or credential.</summary>
    private static async Task<string?> ReadStatusAsync(ManagedCacheTestContext test, string identifier)
    {
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Id == identifier)
            .Select(row => row.Status).SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Requires no new native artifacts or audit, preserving exact interaction state despite separately committed redemption.</summary>
    private static async Task RequireRollbackAsync(ManagedCacheTestContext test, ManagedCacheSnapshot before, string action)
    {
        var after = await test.SnapshotAsync();
        Assert.Equal(before.Tokens.Select(row => row.Id), after.Tokens.Select(row => row.Id));
        Assert.Equal(before.Authorizations, after.Authorizations);
        Assert.Equal(Assert.Single(before.Interactions).State, Assert.Single(after.Interactions).State);
        await using var scope = test.Http.Scope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>()
            .AnyAsync(row => row.Event == action, TestContext.Current.CancellationToken));
    }
}
