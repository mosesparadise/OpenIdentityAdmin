using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks explicit PAR composition installs required ownership and preserves the real native handler descriptors.</summary>
public sealed class NativeIssuanceRegistrationTests
{
    private const string Authorize = "/authorize";
    private const string Token = "/token";
    private const string Par = "/par";

    /// <summary>Both ASP.NET Core registration orders retain exactly one observed native slot and install owned dispatch without a separate prerequisite call.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_ExplicitParComposition_When_HostRegistrationOrderChanges_Then_NativeObservationsAndOwnerRemain(bool hostFirst)
    {
        var services = new ServiceCollection().AddLogging();
        var builder = services.AddOpenIddict().AddServer().AllowAuthorizationCodeFlow().SetAuthorizationEndpointUris(Authorize)
            .SetTokenEndpointUris(Token).SetPushedAuthorizationEndpointUris(Par).AddEphemeralSigningKey().AddEphemeralEncryptionKey();
        if (hostFirst)
        {
            builder.UseAspNetCore();
        }
        builder.AddAdministratorParIssuance();
        if (!hostFirst)
        {
            builder.UseAspNetCore();
        }
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;
        var dispatcher = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IOpenIddictServerDispatcher));
        Assert.Equal(typeof(OwnedServerDispatcher), dispatcher.ImplementationType);
        Require(options, OpenIddictServerHandlers.Protection.CreateTokenEntry.Descriptor, typeof(NativeTokenCreation));
        Require(options, OpenIddictServerHandlers.Protection.GenerateIdentityModelToken.Descriptor, typeof(NativeTokenSigning));
        Require(options, OpenIddictServerHandlers.Protection.AttachTokenPayload.Descriptor, typeof(NativeTokenPayload));
        Require(options, OpenIddictServerAspNetCoreHandlers.ProcessJsonResponse<OpenIddictServerEvents.ApplyPushedAuthorizationResponseContext>.Descriptor,
            typeof(NativeJsonResponse<OpenIddictServerEvents.ApplyPushedAuthorizationResponseContext>));
    }

    /// <summary>Compares original native filters/order/context and rejects an accidentally duplicated unobserved execution slot.</summary>
    private static void Require(OpenIddictServerOptions options, OpenIddictServerHandlerDescriptor native, Type replacement)
    {
        var actual = Assert.Single(options.Handlers, descriptor => descriptor.ServiceDescriptor.ServiceType == replacement);
        Assert.Equal(native.FilterTypes, actual.FilterTypes);
        Assert.Equal(native.Order, actual.Order);
        Assert.Equal(native.ContextType, actual.ContextType);
        Assert.DoesNotContain(options.Handlers, descriptor => descriptor.ServiceDescriptor.ServiceType == native.ServiceDescriptor.ServiceType);
    }
}
