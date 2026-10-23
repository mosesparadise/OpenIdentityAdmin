using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires reached transaction faults and independently reconciled persisted registration/request/audit outcomes.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserUpgradeFailureTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly UpgradeFailure[] Failures = [UpgradeFailure.Audit, UpgradeFailure.Application, UpgradeFailure.Revocation, UpgradeFailure.BeforeCommit, UpgradeFailure.Cancellation, UpgradeFailure.AfterCommit];

    /// <summary>No injected failure reports success; only acknowledgement loss retains the atomic transition and later exact replay writes nothing.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_RealUpgradeWrites_When_AuditOrCommitFails_Then_OutcomeIsReconciledWithoutAutomaticRetry(string provider, UpgradeFailure mode)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var fault = new BrowserUpgradeFault(mode, cancellation);
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider, configure: fault.Configure);
        var input = BrowserUpgradeFixture.Input(scenario);
        var pending = await NativeAccountFlow.PushAsync(scenario);
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(scenario, pending);
        var before = await BrowserUpgradeFixture.ReadAsync(scenario);
        fault.Armed = true;
        try
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() => BrowserUpgradeFixture.UpgradeAsync(scenario, input, token: cancellation.Token));
            Assert.True(fault.Contains(error), error.GetType().FullName);
        }
        finally { fault.Armed = false; }
        Assert.Equal(1, fault.Faults);
        Assert.Equal(mode is UpgradeFailure.Audit or UpgradeFailure.Application or UpgradeFailure.Revocation ? 0 : 1, fault.Attempts);
        Assert.Equal(mode == UpgradeFailure.AfterCommit ? 1 : 0, fault.Commits);
        var after = await BrowserUpgradeFixture.ReadAsync(scenario);
        if (mode == UpgradeFailure.AfterCommit)
        {
            Assert.True(after.Browser && !after.Legacy);
            Assert.Equal(OpenIddictConstants.Statuses.Revoked, await NativeAccountPersistence.StatusAsync(scenario, identifier));
            await BrowserUpgradeEffects.RequireReceiptAsync(scenario, input);
            Assert.False(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
            Assert.True(after == await BrowserUpgradeFixture.ReadAsync(scenario));
        }
        else
        {
            Assert.True(before == after);
            Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, identifier));
            Assert.True(await BrowserUpgradeFixture.UpgradeAsync(scenario, input));
            await BrowserUpgradeEffects.RequireReceiptAsync(scenario, input);
        }
    }

    public static IEnumerable<TheoryDataRow<string, UpgradeFailure>> Cases => from provider in Providers
                                                                              from mode in Failures
                                                                              select new TheoryDataRow<string, UpgradeFailure>(provider, mode).WithTrait(ProviderTrait, provider);
}
