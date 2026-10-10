using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IdentityPlatform.AuthServer.Features.Protocol.Protection;

public static class PilotDatabaseBounds
{
    public static void Apply(PlatformDbContext database)
    {
        var connection = database.Database.GetConnectionString()!;
        database.Database.SetConnectionString(database.Database.IsNpgsql()
            ? new NpgsqlConnectionStringBuilder(connection) { Timeout = 5 }.ConnectionString
            : new SqlConnectionStringBuilder(connection) { ConnectTimeout = 5 }.ConnectionString);
        database.Database.SetCommandTimeout(5);
    }
}
