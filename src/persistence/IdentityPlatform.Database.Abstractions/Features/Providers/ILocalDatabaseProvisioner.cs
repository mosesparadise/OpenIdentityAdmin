namespace IdentityPlatform.Database;

/// <summary>Optional privileged local capability; the Migrator must enforce explicit Development-only authorization first.</summary>
/// <example><code>await provider.LocalProvisioning!.ProvisionAsync(administratorConnection, DatabaseStore.Platform, validatedPassword, token);</code></example>
public interface ILocalDatabaseProvisioner
{
    /// <summary>Uses an isolated unpooled connection and safe logging to establish least-privilege local runtime grants.</summary>
    /// <example><code>await provisioner.ProvisionAsync(administratorConnection, store, validatedPassword, deadline.Token);</code></example>
    Task ProvisionAsync(string connection, DatabaseStore store, string password, CancellationToken cancellationToken);
}
