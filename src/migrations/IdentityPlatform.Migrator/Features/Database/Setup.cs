using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Features.Database;

internal static class Setup
{
    internal static HostApplicationBuilder AddDatabase(this HostApplicationBuilder builder)
    {
        builder.Services.AddPlatformPersistence(
            DatabaseProviderParser.Parse(builder.Configuration["Database:Provider"]),
            builder.Configuration["ConnectionStrings:platform"] ?? "",
            builder.Configuration["ConnectionStrings:adminsessions"] ?? "");
        return builder;
    }

    internal static async Task<int> UpgradeDatabaseAsync(this IHost host)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        return await DatabaseUpgrade.RunAsync(host.Services, deadline.Token,
            host.Services.GetService<LocalRuntimeCredentials>());
    }
}
