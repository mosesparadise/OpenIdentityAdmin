using IdentityPlatform.AppHost.Tests.Features.Samples;

namespace IdentityPlatform.AppHost.Tests.Features.SampleChromium;

/// <summary>Requires the real sample client/API journey on both databases with the retained chromium engine.</summary>
public sealed class SampleRuntimeTests
{
    private const string Engine = "chromium";

    /// <summary>Uses ordinary Aspire startup, manual UI setup, fresh native sign-in, protected resources and disposable restart.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_OrdinaryAspire_When_SampleIntegrationRuns_Then_NativeAuthorityAndDisposalRemainBounded(string provider) =>
        SampleRuntimeScenario.RunAsync(provider, Engine);
}
