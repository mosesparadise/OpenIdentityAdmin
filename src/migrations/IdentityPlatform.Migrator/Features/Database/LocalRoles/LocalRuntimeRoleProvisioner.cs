using IdentityPlatform.Database;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Migrator.Features.Database.LocalRoles;

public enum RuntimeStore
{
    Platform, Sessions
}
/// <summary>Holds disposable Development bootstrap passwords solely for the one-shot local provisioning operation.</summary>
/// <example><code>var credentials = new LocalRuntimeCredentials(platformSecret, sessionSecret); // Never log or persist.</code></example>
public sealed record LocalRuntimeCredentials(string PlatformPassword, string SessionsPassword);

/// <summary>Delegates explicitly authorized Development provisioning to the selected provider.</summary>
/// <example><code>await LocalRuntimeRoleProvisioner.ProvisionAsync(database, RuntimeStore.Platform, password, token);</code></example>
public static class LocalRuntimeRoleProvisioner
{
    private const string MissingCapability = "The selected database provider does not support local provisioning.";

    /// <summary>Rejects unsafe local password literals before privileged DDL.</summary>
    /// <example><code>LocalRuntimeRoleProvisioner.ValidatePassword(generatedPassword);</code></example>
    public static void ValidatePassword(string password) => LocalRolePolicy.ValidatePassword(password);

    /// <summary>Uses fixed logical stores and requires an explicit local provisioning capability.</summary>
    /// <example><code>await LocalRuntimeRoleProvisioner.ProvisionAsync(database, RuntimeStore.Sessions, password, token);</code></example>
    public static Task ProvisionAsync(DbContext database, RuntimeStore store, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ValidatePassword(password);
        var logicalStore = store switch
        {
            RuntimeStore.Platform => DatabaseStore.Platform,
            RuntimeStore.Sessions => DatabaseStore.Sessions,
            _ => throw new ArgumentOutOfRangeException(nameof(store))
        };
        var capability = database.GetDatabaseProvider().LocalProvisioning ?? throw new InvalidOperationException(MissingCapability);
        return capability.ProvisionAsync(database.Database.GetConnectionString()!, logicalStore, password, cancellationToken);
    }
}
