using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Offers explicit coexistence for approved legacy pilot scopes without changing owned-only store registration.</summary>
internal static class ProtocolStorageSelectionSetup
{
    /// <summary>Requires immutable scope selection before resolving any of the four stores captured by native managers.</summary>
    internal static IServiceCollection AddProtocolStorageSelection(this IServiceCollection services)
    {
        services.AddProtocolStorageOwnership();
        services.AddScoped<ProtocolStorageSelection>();
        Add<IOpenIddictApplicationStore<OpenIddictEntityFrameworkCoreApplication>, OwnedApplicationStore, OpenIddictEntityFrameworkCoreApplicationStore>(services);
        Add<IOpenIddictAuthorizationStore<OpenIddictEntityFrameworkCoreAuthorization>, OwnedAuthorizationStore, OpenIddictEntityFrameworkCoreAuthorizationStore>(services);
        Add<IOpenIddictScopeStore<OpenIddictEntityFrameworkCoreScope>, OwnedScopeStore, OpenIddictEntityFrameworkCoreScopeStore>(services);
        Add<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>, OwnedTokenStore, OpenIddictEntityFrameworkCoreTokenStore>(services);
        return services;
    }

    /// <summary>Constructs only the selected store after the scope has frozen its declared execution mode.</summary>
    private static void Add<TStore, TOwned, TLegacy>(IServiceCollection services)
        where TStore : class where TOwned : class, TStore where TLegacy : class, TStore
    {
        services.AddScoped<TOwned>();
        services.AddScoped<TLegacy>();
        services.Replace(ServiceDescriptor.Scoped<TStore>(provider => provider.GetRequiredService<ProtocolStorageSelection>().ResolveOwned()
            ? provider.GetRequiredService<TOwned>() : provider.GetRequiredService<TLegacy>()));
    }
}
