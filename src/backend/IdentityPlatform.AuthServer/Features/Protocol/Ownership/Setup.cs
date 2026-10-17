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
}
