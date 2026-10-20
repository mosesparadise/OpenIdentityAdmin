using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Client;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks the actual opt-in registration preserves installed native descriptor order/filters without duplicate execution.</summary>
public sealed class NativeConsumptionRegistrationTests
{
    private const string AuthorizationPath = "/authorize";
    private const string TokenPath = "/token";
    private const string CallbackPath = "/callback";
    /// <summary>The disabled stack retains both native handlers; opt-in replaces only their pipeline slots, not their services.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_ServerComposition_When_DurableConsumptionSelected_Then_NativeOrderAndFiltersRemain(bool enabled)
    {
        var services = new ServiceCollection().AddLogging();
        var builder = services.AddOpenIddict().AddServer().AllowAuthorizationCodeFlow()
            .SetAuthorizationEndpointUris(AuthorizationPath).SetTokenEndpointUris(TokenPath).AddEphemeralEncryptionKey().AddEphemeralSigningKey();
        if (enabled)
        {
            builder.AddDurableServerConsumption();
        }
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;
        RequireServer(options, OpenIddictServerHandlers.RedeemTokenEntry.Descriptor, typeof(DurableServerRedemption), enabled);
        RequireServer(options, OpenIddictServerHandlers.Protection.ValidateTokenEntry.Descriptor, typeof(DurableServerValidation), enabled);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(OpenIddictServerHandlers.RedeemTokenEntry));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(OpenIddictServerHandlers.Protection.ValidateTokenEntry));
    }

    /// <summary>Early client state consumption retains every native filter and precedes later endpoint/correlation processing.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_ClientComposition_When_DurableConsumptionSelected_Then_NativeEarlySlotRemains(bool enabled)
    {
        var services = new ServiceCollection().AddLogging();
        var builder = services.AddOpenIddict().AddClient().AllowAuthorizationCodeFlow().SetRedirectionEndpointUris(CallbackPath)
            .AddEphemeralEncryptionKey().AddEphemeralSigningKey();
        if (enabled)
        {
            builder.AddDurableClientConsumption();
        }
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenIddictClientOptions>>().Value;
        var native = OpenIddictClientHandlers.RedeemStateTokenEntry.Descriptor;
        var actual = Assert.Single(options.Handlers, descriptor => descriptor.ServiceDescriptor.ServiceType ==
            (enabled ? typeof(DurableClientRedemption) : native.ServiceDescriptor.ServiceType));
        Assert.Equal(native.Order, actual.Order);
        Assert.Equal(native.FilterTypes, actual.FilterTypes);
        Assert.DoesNotContain(options.Handlers, descriptor => descriptor.ServiceDescriptor.ServiceType ==
            (enabled ? native.ServiceDescriptor.ServiceType : typeof(DurableClientRedemption)));
        Assert.True(actual.Order < OpenIddictClientHandlers.ValidateStateTokenEndpointType.Descriptor.Order);
    }

    /// <summary>Both store lock helpers require an explicit owner transaction before any SQL or database resolution can execute.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ReadPhase_When_ProtocolLockRequested_Then_NoAutocommitLockIsAllowed(bool client)
    {
        await using var probe = new StorageProbe();
        await using var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client ?
            AdministratorProtocolLocks.AcquireClientStateAsync(scope.Owner, TestContext.Current.CancellationToken).AsTask() :
            AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Checks exact context/order/filter inheritance while proving the old slot is absent only after opt-in.</summary>
    private static void RequireServer(OpenIddictServerOptions options, OpenIddictServerHandlerDescriptor native, Type adapter, bool enabled)
    {
        var actual = Assert.Single(options.Handlers, descriptor => descriptor.ServiceDescriptor.ServiceType == (enabled ? adapter : native.ServiceDescriptor.ServiceType));
        Assert.Equal(native.Order, actual.Order);
        Assert.Equal(native.FilterTypes, actual.FilterTypes);
        Assert.Equal(native.ContextType, actual.ContextType);
        Assert.DoesNotContain(options.Handlers, descriptor => descriptor.ServiceDescriptor.ServiceType == (enabled ? native.ServiceDescriptor.ServiceType : adapter));
    }
}
