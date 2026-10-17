using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Offers explicit native-dispatch ownership without activating any production endpoint or profile.</summary>
internal static class Setup
{
    /// <summary>Uses the child provider for the real native dispatcher and every handler it resolves.</summary>
    internal static IServiceCollection AddOwnedServerDispatcher(this IServiceCollection services)
    {
        services.TryAddScoped<OpenIddictServerDispatcher>();
        services.Replace(ServiceDescriptor.Scoped<IOpenIddictServerDispatcher, OwnedServerDispatcher>());
        return services;
    }

    /// <summary>Preserves native filter/order metadata while explicitly opting into fresh durable administrator consumption.</summary>
    internal static OpenIddictServerBuilder AddDurableServerConsumption(this OpenIddictServerBuilder builder)
    {
        var redemption = OpenIddictServerHandlers.RedeemTokenEntry.Descriptor;
        var validation = OpenIddictServerHandlers.Protection.ValidateTokenEntry.Descriptor;
        builder.RemoveEventHandler(redemption).RemoveEventHandler(validation);
        builder.Services.AddScoped<OpenIddictServerHandlers.RedeemTokenEntry>();
        builder.Services.AddScoped<OpenIddictServerHandlers.Protection.ValidateTokenEntry>();
        builder.Services.TryAddScoped<OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessSignInContext>()
            .Import(redemption).UseScopedHandler<DurableServerRedemption>().SetType(OpenIddictServerHandlerType.Custom).Build());
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenContext>()
            .Import(validation).UseScopedHandler<DurableServerValidation>().SetType(OpenIddictServerHandlerType.Custom).Build());
        return builder;
    }
}
