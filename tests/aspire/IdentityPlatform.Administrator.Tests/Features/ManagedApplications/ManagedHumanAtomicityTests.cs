using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves actual native internal saves, final managed saves and audit writes remain atomic across every human phase.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedHumanAtomicityTests(BootstrapFixture fixture)
{
    /// <summary>Every causal save mutation is witnessed and rejected; independent exact durable snapshots match before retrying the original proof.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ActualManagedPhases_When_InternalOrFinalPersistenceFails_Then_ProofAuditAndAuthorityRollBack(string provider)
    {
        var fault = new ManagedHumanSaveFault();
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        var form = await ManagedHumanHttp.StartAsync(test);
        var expiry = Assert.Single((await test.SnapshotAsync()).Interactions).ExpiresAt;
        fault.Advance = () => test.Http.Environment.Issuer.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(expiry);
        await RequireFaultsAsync(test, fault, () => ManagedHumanHttp.PasswordAsync(test, form), internalSaves: true);
        using var password = await ManagedHumanHttp.PasswordAsync(test, form);
        await ManagedHumanHttp.EntryAsync(password);
        var mfa = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, password.Headers.Location!);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(test.Http.Environment.Issuer.Account.Journey);
        await RequireFaultsAsync(test, fault, () => ManagedHumanHttp.MfaAsync(test, mfa, code), internalSaves: true);
        using var factor = await ManagedHumanHttp.MfaAsync(test, mfa, code);
        await ManagedHumanHttp.EntryAsync(factor);
        var consent = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, factor.Headers.Location!);
        await RequireFaultsAsync(test, fault, () => ManagedHumanHttp.DecideAsync(test, consent), internalSaves: false);
        using var approved = await ManagedHumanHttp.DecideAsync(test, consent);
        await ManagedHumanHttp.EntryAsync(approved);
        Assert.Equal(ManagedLoginState.Approved, Assert.Single((await test.SnapshotAsync()).Interactions).State);
    }

    /// <summary>Restores only test fault controls; each attempt uses the same genuine browser/native proof and independently checks complete rollback.</summary>
    private static async Task RequireFaultsAsync(ManagedCacheTestContext test, ManagedHumanSaveFault fault,
        Func<Task<HttpResponseMessage>> send, bool internalSaves)
    {
        var before = await ManagedHumanEvidence.DigestAsync(test);
        var now = test.Http.Now;
        foreach (var defect in Enum.GetValues<ManagedHumanSaveDefect>().Where(value => internalSaves || value is not (ManagedHumanSaveDefect.InternalBinding or ManagedHumanSaveDefect.Ceremony)))
        {
            fault.Arm(defect);
            try
            {
                using var denied = await send();
                await ManagedCacheTestContext.UnavailableAsync(denied);
                Assert.True(fault.Fired);
                Assert.True(defect == ManagedHumanSaveDefect.Throw || fault.MutationCompleted);
                Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
            }
            finally
            {
                fault.Selected = null;
                test.Http.Environment.Issuer.Account.Journey.Clock.Now = now;
            }
        }
        await RequireAuditFailureAsync(test, send, before);
    }

    /// <summary>Denies the actual provider audit INSERT privilege and proves the same phase can resume only after restoring it.</summary>
    private static async Task RequireAuditFailureAsync(ManagedCacheTestContext test, Func<Task<HttpResponseMessage>> send, string before)
    {
        var lease = test.Http.Environment.Issuer.Account.Journey.Lease;
        await lease.AuditPermissionAsync(false, TestContext.Current.CancellationToken);
        try
        {
            using var denied = await send();
            await ManagedCacheTestContext.UnavailableAsync(denied);
            Assert.Equal(before, await ManagedHumanEvidence.DigestAsync(test));
        }
        finally { await lease.AuditPermissionAsync(true, TestContext.Current.CancellationToken); }
    }
}
