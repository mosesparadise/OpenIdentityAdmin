using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Requires real native response-field mutations to withhold output and roll back only the later issuance.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeAccountResponseTests(BootstrapFixture fixture)
{
    /// <summary>Pre-formatter credential/identity injection cannot survive sealing even when native signing and storage already ran.</summary>
    [Theory, MemberData(nameof(AdmissionCases.ResponseFields), MemberType = typeof(AdmissionCases))]
    public async Task Given_NativeGeneratedCredential_When_UnexpectedResponseFieldIsAdded_Then_IssuanceRollsBackAndConsumptionRemains(string provider, string fault)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var authorization = fault is NativeAccountResponseFault.AuthorizationRefresh or NativeAccountResponseFault.AuthorizationIdentity;
        var attempt = await NativeAccountAttempt.PrepareAsync(scenario, authorization);
        var control = scenario.Services.GetRequiredService<NativeAccountResponseFault>();
        control.Selected = fault;
        NativeAccountResult response;
        try
        {
            response = await attempt.InvokeAsync();
        }
        finally { control.Selected = null; }
        Assert.Equal(1, control.Observed);
        await attempt.RequireDeniedAsync(response);
    }
}
