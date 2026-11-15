using IdentityPlatform.AppHost.Tests.Features.Samples;

namespace IdentityPlatform.AppHost.Tests.Features.SampleWebKit;

/// <summary>Requires the real sample client/API journey on both databases with the retained webkit engine.</summary>
public sealed class SampleRuntimeTests
{
    private const string Engine = "webkit";

    /// <summary>Uses ordinary Aspire startup, manual UI setup, fresh native sign-in, protected resources and disposable restart.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_OrdinaryAspire_When_SampleIntegrationRuns_Then_NativeAuthorityAndDisposalRemainBounded(string provider) =>
        SampleRuntimeScenario.RunAsync(provider, Engine);
    /// <summary>Clones through the real UI, denies disabled issuance, then requires fresh native consent and independently validated API use.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_ReviewedApplicationClone_When_EnabledAndSignedIn_Then_IndependentCredentialConsentAndApiAccessWork(string provider) =>
        SampleCloneScenario.RunAsync(provider, Engine);
}
