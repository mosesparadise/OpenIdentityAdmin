using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Orders genuine API disable against actual managed code/token issuance using exact provider sessions and real commit callbacks.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedLifecycleRaceTests(BootstrapFixture fixture)
{
    private const string Governance = "AdministratorGovernance";
    private const string Input = "{\"reason\":\"maintenance\",\"confirm\":true}";
    private const string Redeemed = "redeemed";
    private const string InvalidGrant = "invalid_grant";
    private const int ExchangeTokens = 2;
    private const int CaptureSeconds = 5;

    /// <summary>Both commit orders retain early durable redemption; a disable winner prevents issuance, and a prior issuer winner remains prior issuance.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ConsumedManagedProof_When_DisableAndIssuanceCommitInEitherObservedOrder_Then_OnlyPriorIssuanceCanPublish(string provider, bool exchange, bool issuanceFirst)
    {
        var probe = new NativeAuthorityRaceProbe(issuanceFirst, Governance, managed: true);
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, probe.Configure, configureApi: probe.ConfigureApi);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = exchange ? await flow.CodeAsync() : null;
        var before = await test.SnapshotAsync();
        var policy = await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id);
        probe.Endpoint = exchange ? OpenIddictServerEndpointType.Token : OpenIddictServerEndpointType.Authorization;
        var pending = exchange ? flow.ExchangeAsync(code!) : test.SendAsync(flow.Resume);
        var responses = await RaceAsync(test, provider, probe, pending, issuanceFirst);
        using var issuance = responses[issuanceFirst ? 0 : 1];
        using var disable = responses[issuanceFirst ? 1 : 0];
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        probe.RequireCommits();
        var after = await test.SnapshotAsync();
        Assert.Equal(before.Tokens.Length + (issuanceFirst ? exchange ? ExchangeTokens : 1 : 0), after.Tokens.Length);
        Assert.Equal(before.Authorizations + (issuanceFirst && !exchange ? 1 : 0), after.Authorizations);
        var original = Assert.Single(before.Interactions);
        Assert.Equal(Redeemed, after.Tokens.Single(row => row.Id == (exchange ? original.CodeId : original.RequestId)).Status);
        var changed = await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id);
        Assert.False(changed.Policy.Enabled);
        Assert.Equal(policy.Policy.Revision + 1, changed.Policy.Revision);
        Assert.Equal(policy.NativeDigest, changed.NativeDigest);
        Assert.Equal(policy.Policy.CredentialExpiresAt, changed.Policy.CredentialExpiresAt);
        Assert.Single(changed.LifecycleAudits);
        await RequireOutputAsync(test, flow, issuance, exchange, issuanceFirst);
    }

    /// <summary>Pauses after early consumption, then requires the exact peer/winner wait graph before releasing the selected actual commit.</summary>
    private static async Task<HttpResponseMessage[]> RaceAsync(ManagedCacheTestContext test, string provider, NativeAuthorityRaceProbe probe,
        Task<HttpResponseMessage> pending, bool issuanceFirst)
    {
        try
        {
            await probe.Captured.Task.WaitAsync(TimeSpan.FromSeconds(CaptureSeconds), TestContext.Current.CancellationToken);
            var observer = new NativeLockObservation(provider, test.Http.Environment.Issuer.Account.Journey.Lease.AdministratorConnection);
            // Release only the already consumed issuer request into its original owned transaction.
            Func<Task<HttpResponseMessage>> issue = () =>
            {
                probe.ContinueIssuance.TrySetResult();
                return pending;
            };
            Func<Task<HttpResponseMessage>> disable = () => test.Http.UpdateAsync(test.Id, Input, ApplicationVersion.Create(test.Id, 1), direct: true, enabled: false);
            return await NativeBlockedRace.InvokeAsync<HttpResponseMessage>(issuanceFirst ? issue : disable, probe.Committing.Task,
                issuanceFirst ? disable : issue, probe.PeerLock.Task, probe.ReleaseCommit, observer.WaitAsync);
        }
        catch
        {
            probe.ContinueIssuance.TrySetResult();
            probe.ReleaseCommit.TrySetResult();
            try
            {
                using var drained = await pending;
            }
            catch { /* Preserve the primary observation failure after joining the real request. */ }
            throw;
        }
    }

    /// <summary>Validates prior issued tokens cryptographically; a prior issued code cannot exchange after the disable that followed its commit.</summary>
    internal static async Task RequireOutputAsync(ManagedCacheTestContext test, ManagedTokenFlow flow, HttpResponseMessage response, bool exchange, bool issuanceFirst)
    {
        if (!issuanceFirst)
        {
            await ManagedCacheTestContext.UnavailableAsync(response);
            return;
        }
        if (exchange)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            var row = Assert.Single((await test.SnapshotAsync()).Interactions);
            await ManagedTokenOracle.ValidateAsync(test, flow, body.RootElement, row.UserId!);
            await ManagedTokenEvidence.IssuanceAuditAsync(test);
        }
        else
        {
            var code = await flow.ReadCodeAsync(response);
            using var denied = await flow.ExchangeAsync(code);
            await ManagedTokenEvidence.ErrorAsync(denied, HttpStatusCode.BadRequest, InvalidGrant);
        }
    }
}
