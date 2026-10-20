using IdentityPlatform.Administrator.Tests.Fixtures;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Requires warmed independent issuer roots to reread manifest/application/scope policy and deny drift without live repair.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeRegistrationDriftTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];

    /// <summary>Successful peer authorization warms the real registration path; a later external drift prevents exchange and remains unchanged afterward.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_WarmedIssuerAndRealCode_When_StoredRegistrationDrifts_Then_ExchangeDeniesWithoutRepair(string provider, string changeName)
    {
        var change = Enum.Parse<NativeRegistrationDrift.Change>(changeName);
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var peer = scenario.CreatePeer(provider);
        var reference = await NativeAccountFlow.PushAsync(scenario, peer.Services);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference, peer.Services));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var original = await NativeRegistrationDrift.SnapshotAsync(scenario);
        await NativeRegistrationDrift.ApplyAsync(scenario, change);
        var changed = await NativeRegistrationDrift.SnapshotAsync(scenario);
        Assert.True(original != changed);
        var before = await NativeAccountPersistence.CountAsync(scenario);
        var response = await NativeAccountFlow.ExchangeAsync(scenario, code, peer.Services);
        NativeAccountDenialTests.RequireJson(response, OpenIddictConstants.Errors.InvalidClient);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, principal.GetTokenId()!));
        Assert.True(changed == await NativeRegistrationDrift.SnapshotAsync(scenario));
    }

    public static IEnumerable<TheoryDataRow<string, string>> Cases =>
        from provider in Providers
        from change in Enum.GetNames<NativeRegistrationDrift.Change>()
        select new TheoryDataRow<string, string>(provider, change).WithTrait(ProviderTrait, provider);
}
