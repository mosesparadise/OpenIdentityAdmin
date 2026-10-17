using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Offers explicit native-dispatch ownership without activating any production endpoint or profile.</summary>
internal static class Setup
{
    /// <summary>Uses the child provider for the real native dispatcher and every handler it resolves.</summary>
    internal static IServiceCollection AddOwnedClientDispatcher(this IServiceCollection services)
    {
        services.TryAddScoped<OpenIddictClientDispatcher>();
        services.Replace(ServiceDescriptor.Scoped<IOpenIddictClientDispatcher, OwnedClientDispatcher>());
        return services;
    }
    /// <summary>Opts into request-owned scopes for native PAR/token/metadata service calls without replacing the root scope factory.</summary>
    internal static IServiceCollection AddOwnedClientServiceScopes(this IServiceCollection services)
    {
        services.AddScoped<NativeClientActivation>();
        services.TryAddScoped<OpenIddictClientFactory>();
        services.Replace(ServiceDescriptor.Scoped<IOpenIddictClientFactory, OwnedClientFactory>());
        services.Replace(ServiceDescriptor.Singleton(provider => new OpenIddictClientService(new NativeClientProvider(provider))));
        return services;
    }

    /// <summary>Keeps native early-state filters/order while consuming the fixed registration before any remote transport.</summary>
    internal static OpenIddictClientBuilder AddDurableClientConsumption(this OpenIddictClientBuilder builder)
    {
        var redemption = OpenIddictClientHandlers.RedeemStateTokenEntry.Descriptor;
        builder.RemoveEventHandler(redemption);
        builder.Services.AddScoped<OpenIddictClientHandlers.RedeemStateTokenEntry>();
        builder.Services.TryAddScoped<OpenIddictClientHandlers.Protection.ValidateTokenEntry>();
        builder.AddEventHandler(OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.ProcessAuthenticationContext>()
            .Import(redemption).UseScopedHandler<DurableClientRedemption>().SetType(OpenIddictClientHandlerType.Custom).Build());
        return builder;
    }
}
