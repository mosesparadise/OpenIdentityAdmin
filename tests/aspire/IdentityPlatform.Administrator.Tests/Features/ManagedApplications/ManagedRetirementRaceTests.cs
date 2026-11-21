using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Orders genuine API retirement against actual managed code/token issuance using exact provider sessions and real commit callbacks.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedRetirementRaceTests(BootstrapFixture fixture)
{
    private const string Governance = "AdministratorGovernance";
    private const string RetirementSuffix = "/retirement";
    private const string GuidFormat = "D";
    private const string Separator = "/";
    private const string Input = "{\"reason\":\"maintenance\",\"confirm\":true}";
    private const string Redeemed = "redeemed";
    private const string AccessField = "access_token";
    private const int ExchangeTokens = 2;
    private const int CaptureSeconds = 5;

    /// <summary>Separates current issuer authority from offline token validity before the queued worker changes any native grant status.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_UsableManagedAccessToken_When_ApplicationRetiresBeforeCleanup_Then_UserInfoAndNewAuthorizationDenyWithoutNativeChanges(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        using var exchange = await flow.ExchangeAsync(await flow.CodeAsync());
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        using var body = JsonDocument.Parse(await exchange.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var subject = Assert.Single((await test.SnapshotAsync()).Interactions).UserId!;
        var access = body.RootElement.GetProperty(AccessField).GetString()!;
        using var control = await ManagedConsentTest.ReadAsync(test, access);
        await ManagedConsentTest.RequireAsync(control, subject, null, false);
        var accepted = await RetireAsync(test);
        await ManagedTokenOracle.ValidateAsync(test, flow, body.RootElement, subject);
        await RequireIssuerDenialAsync(test, access);
        await GrantIssuerRevocation.ReconcileAsync(test, accepted.Job);
    }

    /// <summary>Retires the current managed registration and independently verifies its committed terminal receipt and preserved configuration.</summary>
    internal static async Task<ApplicationRetirementAcceptance> RetireAsync(ManagedCacheTestContext test)
    {
        var before = await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id);
        using var retirement = await test.Http.CatalogueAsync(HttpMethod.Post,
            ApplicationHttpFixture.ApiCollection + Separator + Uri.EscapeDataString(test.Id) + RetirementSuffix, Input,
            ApplicationVersion.Create(test.Id, before.Policy.Revision), direct: true, key: Guid.NewGuid().ToString(GuidFormat));
        Assert.Equal(HttpStatusCode.Accepted, retirement.StatusCode);
        var accepted = (await retirement.Content.ReadFromJsonAsync<ApplicationRetirementAcceptance>(TestContext.Current.CancellationToken))!;
        await RequireTerminalAsync(test, before, accepted);
        return accepted;
    }

    /// <summary>Checks that the retired client loses UserInfo and new authorization at its own callback before grant cleanup.</summary>
    internal static async Task RequireIssuerDenialAsync(ManagedCacheTestContext test, string access)
    {
        var before = await ManagedUserInfoEvidence.DigestAsync(test);
        using var denied = await ManagedConsentTest.ReadAsync(test, access);
        await ManagedConsentTest.DeniedAsync(denied);
        using var admission = await test.SendAsync(NativeCacheFlow.Initial(NativeCacheProfile.Random(), test.ClientIdentifier, test.CallbackUri));
        await ManagedCacheTestContext.UnavailableAsync(admission);
        Assert.Equal(before, await ManagedUserInfoEvidence.DigestAsync(test));
    }

    /// <summary>Both commit orders retain early durable redemption; a retirement winner prevents issuance, and a prior issuer winner remains prior issuance.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ConsumedManagedProof_When_RetirementAndIssuanceCommitInEitherObservedOrder_Then_OnlyPriorIssuanceCanPublish(string provider, bool exchange, bool issuanceFirst)
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
        using var retirement = responses[issuanceFirst ? 1 : 0];
        Assert.Equal(HttpStatusCode.Accepted, retirement.StatusCode);
        var accepted = (await retirement.Content.ReadFromJsonAsync<ApplicationRetirementAcceptance>(TestContext.Current.CancellationToken))!;
        probe.RequireCommits();
        var after = await test.SnapshotAsync();
        Assert.Equal(before.Tokens.Length + (issuanceFirst ? exchange ? ExchangeTokens : 1 : 0), after.Tokens.Length);
        Assert.Equal(before.Authorizations + (issuanceFirst && !exchange ? 1 : 0), after.Authorizations);
        var original = Assert.Single(before.Interactions);
        Assert.Equal(Redeemed, after.Tokens.Single(row => row.Id == (exchange ? original.CodeId : original.RequestId)).Status);
        await RequireTerminalAsync(test, policy, accepted);
        await ManagedLifecycleRaceTests.RequireOutputAsync(test, flow, issuance, exchange, issuanceFirst);
        probe.Endpoint = null;
        await GrantIssuerRevocation.ReconcileAsync(test, accepted.Job);
    }

    /// <summary>Checks unchanged native configuration and the exact terminal receipt independently of the accepted wire response.</summary>
    private static async Task RequireTerminalAsync(ManagedCacheTestContext test, ApplicationUpdateSnapshot policy, ApplicationRetirementAcceptance accepted)
    {
        var changed = await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id);
        Assert.False(changed.Policy.Enabled);
        Assert.Equal(policy.Policy.Revision + 1, changed.Policy.Revision);
        Assert.Equal(policy.NativeDigest, changed.NativeDigest);
        Assert.Equal(policy.Policy.CredentialExpiresAt, changed.Policy.CredentialExpiresAt);
        Assert.True(changed.Policy.Retired);
        Assert.Empty(changed.LifecycleAudits);
        await using var scope = test.Http.Scope();
        var receipt = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationRetirement>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Guid.Parse(accepted.Job.Id), receipt.GrantJobId);
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
            Func<Task<HttpResponseMessage>> retire = () => test.Http.CatalogueAsync(HttpMethod.Post,
                ApplicationHttpFixture.ApiCollection + Separator + Uri.EscapeDataString(test.Id) + RetirementSuffix, Input,
                ApplicationVersion.Create(test.Id, 1), direct: true, key: Guid.NewGuid().ToString(GuidFormat));
            return await NativeBlockedRace.InvokeAsync<HttpResponseMessage>(issuanceFirst ? issue : retire, probe.Committing.Task,
                issuanceFirst ? retire : issue, probe.PeerLock.Task, probe.ReleaseCommit, observer.WaitAsync);
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

}
