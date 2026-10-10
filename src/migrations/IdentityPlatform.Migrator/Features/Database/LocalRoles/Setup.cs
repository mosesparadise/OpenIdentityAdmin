using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Features.Database.LocalRoles;

internal static class Setup
{
    internal static HostApplicationBuilder AddLocalRoleConfiguration(this HostApplicationBuilder builder)
    {
        var flag = builder.Configuration["LocalDevelopment:ProvisionRuntimeRoles"];
        var provisionLocalRoles = false;
        if (flag is not null && !bool.TryParse(flag, out provisionLocalRoles))
        {
            throw new ArgumentException("Invalid local bootstrap flag.");
        }

        if (provisionLocalRoles)
        {
            if (!builder.Environment.IsDevelopment())
            {
                throw new ArgumentException("Local database bootstrap requires Development.");
            }

            var localCredentials = new LocalRuntimeCredentials(
                builder.Configuration["LocalDevelopment:PlatformPassword"] ?? "",
                builder.Configuration["LocalDevelopment:SessionsPassword"] ?? "");
            LocalRuntimeRoleProvisioner.ValidatePassword(localCredentials.PlatformPassword);
            LocalRuntimeRoleProvisioner.ValidatePassword(localCredentials.SessionsPassword);
            builder.Services.AddSingleton(localCredentials);
        }
        return builder;
    }
}
