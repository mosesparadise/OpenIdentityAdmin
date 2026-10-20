using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Migrator.Features.Operations;

internal static class Setup
{
    private const string OperationSetting = "Operation";
    private const string Upgrade = "upgrade";
    private const string Administrator = "initialize-administrator";
    private const string AdministratorProtocol = "initialize-administrator-protocol";
    private const string InvalidOperation = "Invalid migrator operation.";
    private const string ProvisionRoles = "LocalDevelopment:ProvisionRuntimeRoles";
    private const string Disabled = "false";
    private const string RolesDenied = "Bootstrap cannot provision database roles.";

    /// <summary>Rejects unknown operations instead of silently initializing or upgrading a database.</summary>
    internal static MigratorOperation SelectedOperation(this IConfiguration configuration) => configuration[OperationSetting] switch
    {
        null or Upgrade => MigratorOperation.Upgrade,
        Administrator => MigratorOperation.Administrator,
        AdministratorProtocol => MigratorOperation.AdministratorProtocol,
        _ => throw new ArgumentException(InvalidOperation)
    };

    /// <summary>Preserves the existing administrator bootstrap selection while recognizing the separately explicit protocol initializer.</summary>
    internal static bool IsAdministratorBootstrap(this IConfiguration configuration) => configuration.SelectedOperation() == MigratorOperation.Administrator;

    /// <summary>Keeps initialization logs finite and prevents either initialization operation from also changing runtime privileges.</summary>
    internal static HostApplicationBuilder AddOperationConfiguration(this HostApplicationBuilder builder)
    {
        if (builder.Configuration.SelectedOperation() != MigratorOperation.Upgrade)
        {
            if (builder.Configuration[ProvisionRoles] is not (null or Disabled))
            {
                throw new ArgumentException(RolesDenied);
            }
            // Provider/Identity exceptions may include submitted data. Only fixed command outcomes
            // are emitted for this one-shot operation, never raw provider logging.
            builder.Logging.ClearProviders();
        }
        return builder;
    }
}

/// <summary>Enumerates the mutually exclusive one-shot operations; no initialization is implied by schema upgrade.</summary>
internal enum MigratorOperation
{
    Upgrade, Administrator, AdministratorProtocol
}
