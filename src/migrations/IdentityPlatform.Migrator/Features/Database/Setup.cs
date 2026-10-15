using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Composition;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using IdentityPlatform.Migrator.Features.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Features.Database;

internal static class Setup
{
    private const string ProviderSetting = "Database:Provider";
    private const string PlatformConnectionSetting = "ConnectionStrings:platform";
    private const string SessionConnectionSetting = "ConnectionStrings:adminsessions";

    /// <summary>Selects the host-owned provider registration and configures only the stores required by the requested operation.</summary>
    internal static HostApplicationBuilder AddDatabase(this HostApplicationBuilder builder)
    {
        var provider = DatabaseProviderRegistration.Providers.Select(builder.Configuration[ProviderSetting]);
        var platform = builder.Configuration[PlatformConnectionSetting] ?? "";
        if (builder.Configuration.IsAdministratorBootstrap())
        {
            builder.Services.AddIdentityPersistence(provider, platform);
        }
        else
        {
            builder.Services.AddPlatformPersistence(provider, platform,
                builder.Configuration[SessionConnectionSetting] ?? "");
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
