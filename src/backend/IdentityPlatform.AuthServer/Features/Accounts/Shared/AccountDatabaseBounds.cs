using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Bounds connection establishment and command execution for account requests.</summary>
internal static class AccountDatabaseBounds
{
    /// <summary>Sets provider-native connection/command timeouts without opening a connection; transaction lock policy remains separate.</summary>
    internal static void Apply(PlatformDbContext database)
    {
        var connection = database.Database.GetDbConnection();
        database.GetDatabaseProvider().Connections.Bound(connection, AccountPolicy.DatabaseSeconds, readiness: false);
        database.Database.SetConnectionString(connection.ConnectionString);
        database.Database.SetCommandTimeout(AccountPolicy.DatabaseSeconds);
    }
}
