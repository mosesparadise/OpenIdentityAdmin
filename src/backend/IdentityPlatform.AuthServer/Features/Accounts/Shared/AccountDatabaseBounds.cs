using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Bounds connection establishment and command execution for account requests.</summary>
internal static class AccountDatabaseBounds
{
    /// <summary>Sets provider-native connection/command timeouts without opening a connection; transaction lock policy remains separate.</summary>
    internal static void Apply(PlatformDbContext database)
    {
        var connection = database.Database.GetConnectionString()!;
        database.Database.SetConnectionString(database.Database.IsNpgsql()
            ? new NpgsqlConnectionStringBuilder(connection) { Timeout = AccountPolicy.DatabaseSeconds }.ConnectionString
            : new SqlConnectionStringBuilder(connection) { ConnectTimeout = AccountPolicy.DatabaseSeconds }.ConnectionString);
        database.Database.SetCommandTimeout(AccountPolicy.DatabaseSeconds);
    }
}
