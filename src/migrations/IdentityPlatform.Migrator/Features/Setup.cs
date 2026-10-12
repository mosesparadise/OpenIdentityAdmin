using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Migrator.Features.Bootstrap.Initialize;
using IdentityPlatform.Migrator.Features.Database;
using IdentityPlatform.Migrator.Features.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Features;

internal static class Setup
{
    internal static HostApplicationBuilder AddFeatures(this HostApplicationBuilder builder)
    {
        builder.AddDatabase();
        if (builder.Configuration.IsAdministratorBootstrap())
        {
            builder.Services.AddAdministratorBootstrap();
        }
        return builder;
    }

    internal static Task<int> RunFeaturesAsync(this IHost host)
    {
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        return configuration.IsAdministratorBootstrap()
            ? host.InitializeAdministratorAsync(DatabaseProviderParser.Parse(configuration["Database:Provider"]))
            : host.UpgradeDatabaseAsync();
    }
}
