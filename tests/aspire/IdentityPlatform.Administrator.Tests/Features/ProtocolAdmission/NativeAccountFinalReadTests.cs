using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves final awaited account reads cannot hide later native principal or request mutation before commit.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeAccountFinalReadTests(BootstrapFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly bool[] Endpoints = [true, false];
    private const string ProviderTrait = "Provider";
    /// <summary>Every mutation occurs after real signing/formatting and the actual final session SELECT, then must roll back issuance.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_FormattedNativeCredential_When_FinalAuthorityReadMutatesContinuation_Then_NoCredentialCanPublish(
        string provider, bool authorization, string mutation)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        var attempt = await NativeAccountAttempt.PrepareAsync(scenario, authorization);
        var control = scenario.Services.GetRequiredService<NativeAccountFinalRead>();
        control.Endpoint = authorization ? OpenIddictServerEndpointType.Authorization : OpenIddictServerEndpointType.Token;
        control.Selected = Enum.Parse<NativeAccountFinalRead.Change>(mutation);
        NativeAccountResult response;
        try
        {
            response = await attempt.InvokeAsync();
        }
        finally { control.Clear(); }
        Assert.Equal(1, control.Observed);
        await attempt.RequireDeniedAsync(response);
    }

    /// <summary>Enumerates both actual providers, both issuance endpoints and every finite late-mutation category during native discovery.</summary>
    public static IEnumerable<TheoryDataRow<string, bool, string>> Cases =>
        from provider in Providers
        from authorization in Endpoints
        from mutation in Enum.GetNames<NativeAccountFinalRead.Change>()
        select new TheoryDataRow<string, bool, string>(provider, authorization, mutation).WithTrait(ProviderTrait, provider);
}
