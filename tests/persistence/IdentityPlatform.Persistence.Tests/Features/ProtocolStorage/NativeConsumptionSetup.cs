using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Client;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Exercises actual native dispatch, filters and consumption handlers with prevalidated synthetic principals.</summary>
internal static class NativeConsumptionSetup
{
    /// <summary>Installs only the consumption stage; this fixture deliberately makes no cryptographic interoperability claim.</summary>
    internal static void Configure(IServiceCollection services, DatabaseStore store)
    {
        if (store == DatabaseStore.Sessions)
        {
            ConfigureClient(services);
            return;
        }
        var descriptor = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessSignInContext>()
            .Import(OpenIddictServerHandlers.RedeemTokenEntry.Descriptor).UseScopedHandler<DurableServerRedemption>().Build();
        var validation = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenContext>()
            .Import(OpenIddictServerHandlers.Protection.ValidateTokenEntry.Descriptor).UseScopedHandler<DurableServerValidation>().Build();
        services.Add(descriptor.ServiceDescriptor);
        services.Add(validation.ServiceDescriptor);
        services.AddScoped<NativeConsumptionContinuation>();
        foreach (var filter in descriptor.FilterTypes.Concat(validation.FilterTypes).Distinct())
        {
            services.TryAddScoped(filter);
        }
        services.AddOptions<OpenIddictServerOptions>().Configure<TimeProvider>((options, clock) =>
        {
            options.TimeProvider = clock;
            options.Handlers.Clear();
            options.Handlers.Add(descriptor);
            options.Handlers.Add(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessSignInContext>()
                .UseScopedHandler<NativeConsumptionContinuation>().SetOrder(descriptor.Order + 1).Build());
            options.Handlers.Add(validation);
        });
        services.AddScoped<OpenIddictServerHandlers.RedeemTokenEntry>();
        services.AddScoped<OpenIddictServerHandlers.Protection.ValidateTokenEntry>();
        services.AddScoped<OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry>();
        services.AddScoped<IOpenIddictServerFactory, OpenIddictServerFactory>();
        services.AddOwnedServerDispatcher();
    }

    /// <summary>Retains native early callback filters and state validation without running discovery or test network transport.</summary>
    private static void ConfigureClient(IServiceCollection services)
    {
        var descriptor = OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.ProcessAuthenticationContext>()
            .Import(OpenIddictClientHandlers.RedeemStateTokenEntry.Descriptor).UseScopedHandler<DurableClientRedemption>().Build();
        services.Add(descriptor.ServiceDescriptor);
        var endpoint = OpenIddictClientHandlers.ValidateStateTokenEndpointType.Descriptor;
        var error = OpenIddictClientHandlers.HandleFrontchannelErrorResponse.Descriptor;
        services.Add(endpoint.ServiceDescriptor);
        services.Add(error.ServiceDescriptor);
        services.AddScoped<NativeConsumptionContinuation>();
        foreach (var filter in descriptor.FilterTypes.Concat(endpoint.FilterTypes).Concat(error.FilterTypes).Distinct())
        {
            services.TryAddScoped(filter);
        }
        services.AddOptions<OpenIddictClientOptions>().Configure<TimeProvider>((options, clock) =>
        {
            options.TimeProvider = clock;
            options.Handlers.Clear();
            options.Handlers.Add(descriptor);
            options.Handlers.Add(OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.ProcessAuthenticationContext>()
                .UseScopedHandler<NativeConsumptionContinuation>().SetOrder(descriptor.Order + 1).Build());
            options.Handlers.Add(endpoint);
            options.Handlers.Add(error);
        });
        services.AddScoped<OpenIddictClientHandlers.RedeemStateTokenEntry>();
        services.AddScoped<OpenIddictClientHandlers.Protection.ValidateTokenEntry>();
        services.AddScoped<IOpenIddictClientFactory, OpenIddictClientFactory>();
        services.AddOwnedClientDispatcher();
    }
}
