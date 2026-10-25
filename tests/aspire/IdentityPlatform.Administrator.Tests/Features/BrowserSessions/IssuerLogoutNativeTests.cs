using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Rejects actual native context and output substitutions after genuine issuer revocation without publishing attacker-selected returns.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerLogoutNativeTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private static readonly IssuerLogoutNativeFault[] Variants = [IssuerLogoutNativeFault.ValidatedDestination, IssuerLogoutNativeFault.ResponseField, IssuerLogoutNativeFault.LateOutput, IssuerLogoutNativeFault.Cancellation];

    /// <summary>Targets independent native destination, response field, cancellation and late output boundaries.</summary>
    public static IEnumerable<TheoryDataRow<string, IssuerLogoutNativeFault>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                                       from variant in Variants
                                                                                       select new TheoryDataRow<string, IssuerLogoutNativeFault>(provider, variant).WithTrait(ProviderTrait, provider);

    /// <summary>A modified native continuation cannot turn a real committed logout into an unverified redirect or secret-bearing response.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_CommittedIssuerLogout_When_NativeDestinationOrOutputIsSubstituted_Then_AllSuccessMaterialIsWithheld(string provider, IssuerLogoutNativeFault mode)
    {
        var probe = new IssuerLogoutNativeProbe(mode);
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider, probe.Configure);
        var form = await scenario.FormAsync();
        probe.Arm();
        using var response = await scenario.PostAsync(form);
        await IssuerLogoutTests.RequireUnavailableAsync(response);
        Assert.Equal(1, probe.Observed);
        Assert.Equal(1, probe.Fault.Commits);
        await scenario.RequireRevokedAsync();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(!body.Contains(IssuerLogoutNativeProbe.PrivateMarker, StringComparison.Ordinal) && !body.Contains(IssuerLogoutNativeProbe.Foreign, StringComparison.Ordinal));
    }
}

/// <summary>Names distinct native response trust boundaries without depending on arbitrary numeric test modes.</summary>
public enum IssuerLogoutNativeFault
{
    ValidatedDestination, ResponseField, LateOutput, Cancellation
}
