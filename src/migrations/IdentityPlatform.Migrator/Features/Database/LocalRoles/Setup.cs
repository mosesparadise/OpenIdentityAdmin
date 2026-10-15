using IdentityPlatform.Migrator.Composition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Features.Database.LocalRoles;

internal static class Setup
{
    private const string ProviderSetting = "Database:Provider";
    private const string ProvisionSetting = "LocalDevelopment:ProvisionRuntimeRoles";
    private const string InvalidFlag = "Invalid local bootstrap flag.";
    private const string DevelopmentRequired = "Local database bootstrap requires Development.";
    private const string MissingCapability = "The selected database provider does not support local provisioning.";
    private const string PlatformPasswordSetting = "LocalDevelopment:PlatformPassword";
    private const string SessionsPasswordSetting = "LocalDevelopment:SessionsPassword";

    /// <summary>Validates explicit Development-only provisioning and required provider capability before storing disposable credentials.</summary>
    internal static HostApplicationBuilder AddLocalRoleConfiguration(this HostApplicationBuilder builder)
    {
        var flag = builder.Configuration[ProvisionSetting];
        var provisionLocalRoles = false;
        if (flag is not null && !bool.TryParse(flag, out provisionLocalRoles))
        {
            throw new ArgumentException(InvalidFlag);
        }

        if (provisionLocalRoles)
        {
            if (!builder.Environment.IsDevelopment())
            {
                throw new ArgumentException(DevelopmentRequired);
            }

            var provider = DatabaseProviderRegistration.Providers.Select(builder.Configuration[ProviderSetting]);
            if (provider.LocalProvisioning is null)
            {
                throw new ArgumentException(MissingCapability);
            }
            var localCredentials = new LocalRuntimeCredentials(
                builder.Configuration[PlatformPasswordSetting] ?? "",
                builder.Configuration[SessionsPasswordSetting] ?? "");
            LocalRuntimeRoleProvisioner.ValidatePassword(localCredentials.PlatformPassword);
            LocalRuntimeRoleProvisioner.ValidatePassword(localCredentials.SessionsPassword);
            builder.Services.AddSingleton(localCredentials);
        }
        return builder;
    }
}
