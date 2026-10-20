using IdentityPlatform.Administrator.Tests.Fixtures;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Requires HTTP denial for genuine codes whose native authorization is revoked or detached/deleted without changing their protected payload.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeAuthorizationStateTests(BootstrapFixture fixture)
{
    /// <summary>Revocation reaches native authorization status; an orphan rejects its missing restored binding before the authorization lookup.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealCodeWithChangedAuthorization_When_PeerExchangesIt_Then_DenialPreservesCodeAndUnrelatedGrant(string provider, bool orphan)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var peer = scenario.CreatePeer(provider);
        var unrelatedReference = await NativeAccountFlow.PushAsync(scenario);
        var unrelatedCode = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, unrelatedReference, peer.Services));
        var unrelated = await NativeAccountFlow.ValidateAsync(scenario, unrelatedCode, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference, peer.Services));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        Assert.True(principal.GetAuthorizationId() != unrelated.GetAuthorizationId());
        var binding = await NativeAuthorizationState.CodeBindingAsync(scenario, principal.GetTokenId()!);
        await NativeAuthorizationState.ChangeAsync(scenario, principal.GetTokenId()!, principal.GetAuthorizationId()!, orphan);
        await NativeAuthorizationState.RequireChangedAsync(scenario, principal.GetTokenId()!, principal.GetAuthorizationId()!, orphan);
        Assert.True(binding == await NativeAuthorizationState.CodeBindingAsync(scenario, principal.GetTokenId()!));
        var before = await NativeAccountPersistence.CountAsync(scenario);
        NativeAccountDenialTests.RequireJson(await NativeAccountFlow.ExchangeAsync(scenario, code, peer.Services), OpenIddictConstants.Errors.InvalidGrant);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, principal.GetTokenId()!));
        Assert.True(binding == await NativeAuthorizationState.CodeBindingAsync(scenario, principal.GetTokenId()!));
        await NativeMutationGrant.RequireAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, unrelatedCode, peer.Services), unrelated, before);
        await NativeAuthorizationState.RequireChangedAsync(scenario, principal.GetTokenId()!, principal.GetAuthorizationId()!, orphan);
    }
}
