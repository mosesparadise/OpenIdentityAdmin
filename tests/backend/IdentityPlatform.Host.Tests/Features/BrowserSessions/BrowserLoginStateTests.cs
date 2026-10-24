using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Host.Tests.Features.AdministratorClient;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Denies caller-created product provenance on an otherwise valid ordinary native challenge.</summary>
public sealed class BrowserLoginStateTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string InjectedValue = "caller-supplied-reservation";

    /// <summary>Each exact or aliased private receipt claim requires a genuine product reservation before persistence.</summary>
    [Theory]
    [InlineData("oi_b3_browser")]
    [InlineData("oi_b3_generation")]
    [InlineData("oi_b3_reservation")]
    [InlineData("OI_B3_BROWSER")]
    public async Task Given_OrdinaryNativeChallenge_When_ProductReceiptClaimIsInjected_Then_StatePersistenceGuardDenies(string claim)
    {
        using var keys = NativeClientProfileTests.Load(fixture);
        await using var services = NativeClientCompositionTests.Create(keys);
        var context = BrowserLoginClientTransportTests.Context(services);
        await new OpenIddictClientHandlers.PrepareLoginStateTokenPrincipal().HandleAsync(context);
        context.StateTokenPrincipal!.SetClaim(BrowserSessionIntent.ClaimType, (string?)null);
        var profile = services.GetRequiredService<NativeClientProfile>();
        NativeClientChallenge.RequireState(context, profile);
        context.StateTokenPrincipal!.SetClaim(claim, InjectedValue);
        Assert.Throws<InvalidOperationException>(() => NativeClientChallenge.RequireState(context, profile));
    }
}
