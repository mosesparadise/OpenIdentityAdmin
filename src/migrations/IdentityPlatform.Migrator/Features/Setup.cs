using IdentityPlatform.Migrator.Composition;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;
using IdentityPlatform.Migrator.Features.Bootstrap.Initialize;
using IdentityPlatform.Migrator.Features.Database;
using IdentityPlatform.Migrator.Features.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Features;

internal static class Setup
{
    private const string ProviderSetting = "Database:Provider";

    /// <summary>Composes storage and only the explicitly requested administrator bootstrap services.</summary>
    internal static HostApplicationBuilder AddFeatures(this HostApplicationBuilder builder)
    {
        builder.AddDatabase();
        if (builder.Configuration.IsAdministratorBootstrap())
        {
            builder.Services.AddAdministratorBootstrap();
        }
        else if (builder.Configuration.SelectedOperation() is MigratorOperation.AdministratorProtocol or MigratorOperation.BrowserRegistration)
        {
            builder.Services.AddAdministratorProtocolInitialization();
        }
        return builder;
    }

    /// <summary>Runs the selected bounded bootstrap or upgrade operation with the same registered provider.</summary>
    internal static Task<int> RunFeaturesAsync(this IHost host)
    {
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        return configuration.SelectedOperation() switch
        {
            MigratorOperation.Administrator => host.InitializeAdministratorAsync(DatabaseProviderRegistration.Providers.Select(configuration[ProviderSetting])),
            MigratorOperation.AdministratorProtocol => host.InitializeAdministratorProtocolAsync(DatabaseProviderRegistration.Providers.Select(configuration[ProviderSetting])),
            MigratorOperation.BrowserRegistration => host.UpgradeAdministratorBrowserRegistrationAsync(DatabaseProviderRegistration.Providers.Select(configuration[ProviderSetting])),
            _ => host.UpgradeDatabaseAsync()
        };
    }
}
