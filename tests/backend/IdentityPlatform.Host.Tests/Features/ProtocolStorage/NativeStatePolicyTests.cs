using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks fixed state bounds and actual native generation slot composition independently of database execution.</summary>
public sealed class NativeStatePolicyTests
{
    private const string Client = "identity-admin-bff";
    private const string Callback = "/callback";
    private const long Epoch = 1800000000;

    /// <summary>Rejects exact-full and malformed counts before reserving one new native state row.</summary>
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(9999, 1023, true)]
    [InlineData(10000, 1023, false)]
    [InlineData(9999, 1024, false)]
    [InlineData(10001, 0, false)]
    [InlineData(1, 2, false)]
    [InlineData(-1, 0, false)]
    [InlineData(1, -1, false)]
    [InlineData(long.MaxValue, 0, false)]
    public void Given_ObservedNativeCounts_When_ReservingOneState_Then_ExactHardBoundsApply(long retained, long live, bool allowed) =>
        Assert.Equal(allowed, new AdministratorStateCounts(retained, live).CanCreate);

    /// <summary>Exercises expiry equality, future timestamps and lifetime widening independently from native validation skew.</summary>
    [Theory]
    [InlineData(0, 300, true)]
    [InlineData(-300, 0, false)]
    [InlineData(-299, 1, true)]
    [InlineData(0, 301, false)]
    [InlineData(30, 300, true)]
    [InlineData(31, 300, false)]
    [InlineData(0, 0, false)]
    public void Given_PreparedStateDates_When_ProductChecksFreshness_Then_OnlyBoundedCurrentStateQualifies(int creation, int expiration, bool allowed)
    {
        var clock = new StateClock();
        var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetClaim(OpenIddictConstants.Claims.Private.RegistrationId, Client)
            .SetCreationDate(clock.GetUtcNow().AddSeconds(creation)).SetExpirationDate(clock.GetUtcNow().AddSeconds(expiration));
        Assert.Equal(allowed, DurableClientState.IsCurrent(principal, clock));
    }

    /// <summary>Requires the exact installed slot and every native filter, with real delegated handlers still resolvable.</summary>
    [Fact]
    public void Given_OptInNativeStateCreation_When_InspectingConfiguredPipeline_Then_PreservesAllNativeSlotsAndPrecedesPar()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddOpenIddict().AddClient().AllowAuthorizationCodeFlow().SetRedirectionEndpointUris(Callback)
            .AddEphemeralEncryptionKey().AddEphemeralSigningKey().AddDurableClientState();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenIddictClientOptions>>().Value;
        RequireSlot(options, OpenIddictClientHandlers.GenerateLoginStateToken.Descriptor, typeof(DurableClientState));
        RequireSlot(options, OpenIddictClientHandlers.Protection.AttachSecurityCredentials.Descriptor, typeof(NativeStateCredentials));
        RequireSlot(options, OpenIddictClientHandlers.Protection.AttachTokenSubject.Descriptor, typeof(NativeStateSubject));
        RequireSlot(options, OpenIddictClientHandlers.Protection.AttachTokenMetadata.Descriptor, typeof(NativeStateMetadata));
        RequireSlot(options, OpenIddictClientHandlers.Protection.CreateTokenEntry.Descriptor, typeof(NativeStateCreation));
        RequireSlot(options, OpenIddictClientHandlers.Protection.GenerateIdentityModelToken.Descriptor, typeof(NativeStateSigning));
        RequireSlot(options, OpenIddictClientHandlers.Protection.AttachTokenPayload.Descriptor, typeof(NativeStatePayload));
        Assert.True(OpenIddictClientHandlers.GenerateLoginStateToken.Descriptor.Order < OpenIddictClientHandlers.SendPushedAuthorizationRequest.Descriptor.Order);
    }

    /// <summary>Compares against the installed descriptor instead of hardcoding event order or filter names.</summary>
    private static void RequireSlot(OpenIddictClientOptions options, OpenIddictClientHandlerDescriptor native, Type observer)
    {
        var actual = Assert.Single(options.Handlers, handler => handler.ServiceDescriptor.ServiceType == observer);
        Assert.Equal(native.Order, actual.Order);
        Assert.Equal(native.ContextType, actual.ContextType);
        Assert.Equal(native.FilterTypes, actual.FilterTypes);
        Assert.DoesNotContain(options.Handlers, handler => handler.ServiceDescriptor.ServiceType == native.ServiceDescriptor.ServiceType);
    }

    /// <summary>Provides a stable UTC sample without extending any request or transaction timer.</summary>
    private sealed class StateClock : TimeProvider
    {
        /// <summary>Returns the independently selected fixture epoch.</summary>
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(Epoch);
    }
}
