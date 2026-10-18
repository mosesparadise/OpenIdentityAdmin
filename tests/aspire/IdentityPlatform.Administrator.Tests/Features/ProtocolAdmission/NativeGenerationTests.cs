using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Rejects real native generation mutations while preserving durable earlier consumption and withholding all response output.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeGenerationTests(BootstrapFixture fixture)
{
    /// <summary>Each independently reached attack rolls back later issuance on both providers after a genuine account login and PAR.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_NativeCodeGeneration_When_PreparedPrincipalOrCryptoDescriptorChanges_Then_IssuanceRollsBackWithoutOutput(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var control = scenario.Services.GetRequiredService<NativeGenerationFault>();
        foreach (var fault in Enum.GetValues<NativeGenerationFault.Kind>().Where(value => value != NativeGenerationFault.Kind.None))
        {
            var attempt = await NativeAccountAttempt.PrepareAsync(scenario, authorization: true);
            control.Selected = fault;
            control.Observed = 0;
            NativeAccountResult response;
            try
            {
                response = await attempt.InvokeAsync();
            }
            finally { control.Selected = NativeGenerationFault.Kind.None; }
            Assert.Equal(1, control.Observed);
            await attempt.RequireDeniedAsync(response);
        }
    }
}
