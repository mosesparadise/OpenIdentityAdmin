using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Exercises the installed native authentication and refusal handlers with the actual emitted public device and user references.</summary>
internal static class NativeDeviceVerificationEvidence
{
    private const int BackedOffInterval = 10;
    private const int NextInterval = 20;

    /// <summary>Observes a paced pending poll, resolves the human code separately and commits the native denial before retrying.</summary>
    internal static async Task RequireDenialAsync(ManagedSchemaFixture fixture, string device, string user)
    {
        var clock = Assert.IsType<NativeConsumptionClock>(fixture.Services.GetRequiredService<TimeProvider>());
        var start = clock.GetUtcNow();
        clock.Set(start.AddSeconds(BackedOffInterval));
        await RequirePollAsync(fixture, device, OpenIddictConstants.Errors.AuthorizationPending);
        var factory = fixture.Services.GetRequiredService<IServiceScopeFactory>();
        var request = new ProtocolStorageRequest(clock, fixture.Token);
        await request.RunAsync(factory, async (scope, token) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            var native = await CreateAsync(scope, OpenIddictServerEndpointType.EndUserVerification, new()
            {
                UserCode = user
            });
            var dispatcher = scope.Services.GetRequiredService<IOpenIddictServerDispatcher>();
            var authentication = new OpenIddictServerEvents.ProcessAuthenticationContext(native) { CancellationToken = token };
            await dispatcher.DispatchAsync(authentication);
            Assert.False(authentication.IsRejected);
            Assert.NotNull(authentication.UserCodePrincipal);
            native.SetProperty(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!, authentication);
            var denial = new OpenIddictServerEvents.ProcessChallengeContext(native) { CancellationToken = token };
            await dispatcher.DispatchAsync(denial);
            Assert.True(denial.IsRequestHandled);
            await transaction.CommitAsync(token);
            return true;
        });
        clock.Set(start.AddSeconds(NextInterval));
        await RequirePollAsync(fixture, device, OpenIddictConstants.Errors.AccessDenied);
        clock.Set(start);
    }

    /// <summary>Performs the product's committed cadence step and then runs genuine OpenIddict token authentication.</summary>
    private static async Task RequirePollAsync(ManagedSchemaFixture fixture, string device, string expected)
    {
        var factory = fixture.Services.GetRequiredService<IServiceScopeFactory>();
        var request = new ProtocolStorageRequest(fixture.Services.GetRequiredService<TimeProvider>(), fixture.Token);
        var admission = await request.RunAsync(factory, (scope, token) => DevicePollAdmission.PollAsync(scope, NativeDevicePollEvidence.Client, device, token));
        Assert.Equal(DevicePollResult.Admitted, admission);
        await request.RunAsync(factory, async (scope, token) =>
        {
            var native = await CreateAsync(scope, OpenIddictServerEndpointType.Token,
                new()
                {
                    ClientId = NativeDevicePollEvidence.Client,
                    DeviceCode = device,
                    GrantType = OpenIddictConstants.GrantTypes.DeviceCode
                });
            var auth = new OpenIddictServerEvents.ProcessAuthenticationContext(native) { CancellationToken = token };
            await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(auth);
            Assert.True(auth.IsRejected);
            Assert.Equal(expected, auth.Error);
            return true;
        });
    }

    /// <summary>Uses a fresh native transaction per device or user request with the fixture's real issuer, endpoint and opaque reference.</summary>
    internal static async ValueTask<OpenIddictServerTransaction> CreateAsync(ProtocolStorageScope scope, OpenIddictServerEndpointType endpoint, OpenIddictRequest request)
    {
        var native = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
        native.EndpointType = endpoint;
        native.BaseUri = NativeDevicePollEvidence.Issuer;
        native.RequestUri = new(native.BaseUri, endpoint == OpenIddictServerEndpointType.Token ? NativeDevicePollEvidence.Tokens : NativeDevicePollEvidence.Verification);
        native.Request = request;
        native.Response = new OpenIddictResponse();
        return native;
    }
}
