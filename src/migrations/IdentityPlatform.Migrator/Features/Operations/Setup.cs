using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Migrator.Features.Operations;

internal static class Setup
{
    internal static bool IsAdministratorBootstrap(this IConfiguration configuration) => configuration["Operation"] switch
    {
        null or "upgrade" => false,
        "initialize-administrator" => true,
        _ => throw new ArgumentException("Invalid migrator operation.")
    };

    internal static HostApplicationBuilder AddOperationConfiguration(this HostApplicationBuilder builder)
    {
        if (builder.Configuration.IsAdministratorBootstrap())
        {
            if (builder.Configuration["LocalDevelopment:ProvisionRuntimeRoles"] is not (null or "false"))
            {
                throw new ArgumentException("Bootstrap cannot provision database roles.");
            }
            // Provider/Identity exceptions may include submitted data. Only fixed command outcomes
            // are emitted for this one-shot operation, never raw provider logging.
            builder.Logging.ClearProviders();
        }
        return builder;
    }
}
