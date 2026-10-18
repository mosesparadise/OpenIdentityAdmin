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

    /// <summary>Preserves the installed native state-generation slot while moving its manager writes into fresh committed Sessions ownership.</summary>
    internal static OpenIddictClientBuilder AddDurableClientState(this OpenIddictClientBuilder builder)
    {
        ReplaceGeneration<OpenIddictClientHandlers.Protection.AttachSecurityCredentials, NativeStateCredentials>(builder,
            OpenIddictClientHandlers.Protection.AttachSecurityCredentials.Descriptor);
        ReplaceGeneration<OpenIddictClientHandlers.Protection.AttachTokenSubject, NativeStateSubject>(builder,
            OpenIddictClientHandlers.Protection.AttachTokenSubject.Descriptor);
        ReplaceGeneration<OpenIddictClientHandlers.Protection.AttachTokenMetadata, NativeStateMetadata>(builder,
            OpenIddictClientHandlers.Protection.AttachTokenMetadata.Descriptor);
        var generation = OpenIddictClientHandlers.GenerateLoginStateToken.Descriptor;
        builder.RemoveEventHandler(generation);
        builder.Services.AddScoped<OpenIddictClientHandlers.GenerateLoginStateToken>();
        builder.AddEventHandler(OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.ProcessChallengeContext>()
            .Import(generation).UseScopedHandler<DurableClientState>().SetType(OpenIddictClientHandlerType.Custom).Build());
        ReplaceGeneration<OpenIddictClientHandlers.Protection.CreateTokenEntry, NativeStateCreation>(builder,
            OpenIddictClientHandlers.Protection.CreateTokenEntry.Descriptor);
        ReplaceGeneration<OpenIddictClientHandlers.Protection.GenerateIdentityModelToken, NativeStateSigning>(builder,
            OpenIddictClientHandlers.Protection.GenerateIdentityModelToken.Descriptor);
        ReplaceGeneration<OpenIddictClientHandlers.Protection.AttachTokenPayload, NativeStatePayload>(builder,
            OpenIddictClientHandlers.Protection.AttachTokenPayload.Descriptor);
        return builder;
    }

    /// <summary>Preserves native descriptor filters/order and explicit delegated service registration after removing its pipeline slot.</summary>
    private static void ReplaceGeneration<TNative, TObserver>(OpenIddictClientBuilder builder, OpenIddictClientHandlerDescriptor descriptor)
        where TNative : class, IOpenIddictClientHandler<OpenIddictClientEvents.GenerateTokenContext>
        where TObserver : class, IOpenIddictClientHandler<OpenIddictClientEvents.GenerateTokenContext>
    {
        builder.RemoveEventHandler(descriptor);
        builder.Services.AddScoped<TNative>();
        builder.AddEventHandler(OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.GenerateTokenContext>()
            .Import(descriptor).UseScopedHandler<TObserver>().SetType(OpenIddictClientHandlerType.Custom).Build());
    }
}
