using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using IdentityPlatform.Migrator.Features.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Features.Database;

internal static class Setup
{
    internal static HostApplicationBuilder AddDatabase(this HostApplicationBuilder builder)
    {
        var provider = DatabaseProviderParser.Parse(builder.Configuration["Database:Provider"]);
        var platform = builder.Configuration["ConnectionStrings:platform"] ?? "";
        if (builder.Configuration.IsAdministratorBootstrap())
        {
            builder.Services.AddIdentityPersistence(provider, platform);
        }
        else
        {
            builder.Services.AddPlatformPersistence(provider, platform,
                builder.Configuration["ConnectionStrings:adminsessions"] ?? "");
        }
        return builder;
    }

    internal static async Task<int> UpgradeDatabaseAsync(this IHost host)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        return await DatabaseUpgrade.RunAsync(host.Services, deadline.Token,
            host.Services.GetService<LocalRuntimeCredentials>());
    }
}
