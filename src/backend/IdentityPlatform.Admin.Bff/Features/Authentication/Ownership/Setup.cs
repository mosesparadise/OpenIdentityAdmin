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
}
