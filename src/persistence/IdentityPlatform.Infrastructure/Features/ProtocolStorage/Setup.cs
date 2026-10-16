using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Explicitly enables guarded native stores only in a composition whose caller owns ProtocolStorageScope.</summary>
internal static class Setup
{
    /// <summary>Preserves the selected native context and manager filtering while replacing only database-capable store boundaries.</summary>
    internal static IServiceCollection AddProtocolStorageOwnership(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ProtocolStorageOwner>();
        services.AddOpenIddict().AddCore(core =>
        {
            core.DisableEntityCaching();
            core.ReplaceApplicationStore<OpenIddictEntityFrameworkCoreApplication, OwnedApplicationStore>();
            core.ReplaceAuthorizationStore<OpenIddictEntityFrameworkCoreAuthorization, OwnedAuthorizationStore>();
            core.ReplaceScopeStore<OpenIddictEntityFrameworkCoreScope, OwnedScopeStore>();
            core.ReplaceTokenStore<OpenIddictEntityFrameworkCoreToken, OwnedTokenStore>();
        });
        return services;
    }
}
