using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Opts the protected AuthServer into native factor services after its durable issuer protection is validated.</summary>
/// <example><code>services.AddAdministratorFactors(validatedIssuer); // Register only after the verified durable Data Protection ring.</code></example>
public static class Setup
{
    /// <summary>Registers scoped native factor adapters without creating or replacing the host's key ring.</summary>
    /// <param name="services">Existing platform persistence and verified Data Protection registrations.</param>
    /// <param name="issuer">The canonical HTTPS issuer obtained from validated issuer credentials.</param>
    /// <returns>The collection for explicit feature composition.</returns>
    /// <example><code>services.AddAdministratorFactors(credentials.Issuer);</code></example>
    public static IServiceCollection AddAdministratorFactors(this IServiceCollection services, Uri issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        if (!issuer.IsAbsoluteUri || issuer.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(issuer.UserInfo) ||
            !string.IsNullOrEmpty(issuer.Query) || !string.IsNullOrEmpty(issuer.Fragment))
        {
            throw new FactorUnavailableException();
        }
        services.AddSingleton(new FactorProtectionOptions(issuer));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<FactorProtection>();
        services.AddScoped<FactorAccess>();
        services.AddScoped<NativeOperation>();
        services.AddScoped<ProtectedRecoveryStore>();
        services.Replace(ServiceDescriptor.Scoped<FactorStore, ProtectedFactorStore>());
        services.AddIdentityCore<PlatformUser>().AddUserManager<AdministratorUserManager>()
            .AddTokenProvider<AuthenticatorTokenProvider<PlatformUser>>(TokenOptions.DefaultAuthenticatorProvider);
        return services;
    }
}
