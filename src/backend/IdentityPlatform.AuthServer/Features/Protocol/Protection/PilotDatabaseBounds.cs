using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.AuthServer.Features.Protocol.Protection;

/// <summary>Preserves the pilot API for the shared account connection and command bounds.</summary>
/// <example><code>PilotDatabaseBounds.Apply(database);</code></example>
public static class PilotDatabaseBounds
{
    /// <summary>Applies five-second provider connection/command bounds without opening a connection.</summary>
    /// <example><code>PilotDatabaseBounds.Apply(database);</code></example>
    public static void Apply(PlatformDbContext database) => AccountDatabaseBounds.Apply(database);
}
