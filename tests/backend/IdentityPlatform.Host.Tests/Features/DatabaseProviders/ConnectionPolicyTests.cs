using IdentityPlatform.Database;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace IdentityPlatform.Host.Tests.Features.DatabaseProviders;

/// <summary>Checks exact engine commands and connection policies without opening a database connection.</summary>
public sealed class ConnectionPolicyTests
{
    private const int RequestSeconds = 5;
    private const int ReadinessSeconds = 3;

    /// <summary>PostgreSQL keeps precise session restoration and isolated readiness settings.</summary>
    [Fact]
    public void Given_PostgreSqlPolicy_When_CommandsAndBoundsAreApplied_Then_ExistingSemanticsRemain()
    {
        const string Provider = "PostgreSql";
        const string Previous = "13ms";
        var policy = TestDatabaseProviders.Resolve(Provider).Connections;
        using var connection = new NpgsqlConnection(TestDatabaseProviders.Resolve(Provider).DesignTimeConnection);
        using var command = connection.CreateCommand();
        policy.ReadLockTimeout(command);
        Assert.Equal("SELECT current_setting('lock_timeout')", command.CommandText);
        policy.SetLockTimeout(command);
        Assert.Equal("SET lock_timeout = '5s'", command.CommandText);
        policy.RestoreLockTimeout(command, Previous);
        Assert.Equal("SELECT set_config('lock_timeout', @previous, false)", command.CommandText);
        Assert.Equal("13ms", Assert.Single(command.Parameters.Cast<NpgsqlParameter>()).Value);
        Assert.Equal("SET LOCAL lock_timeout = '5s'", policy.TransactionLockLimitSql);
        policy.Bound(connection, RequestSeconds, readiness: false);
        Assert.Equal(5, new NpgsqlConnectionStringBuilder(connection.ConnectionString).Timeout);
        Assert.True(new NpgsqlConnectionStringBuilder(connection.ConnectionString).Pooling);
        policy.Bound(connection, ReadinessSeconds, readiness: true);
        Assert.Equal(3, new NpgsqlConnectionStringBuilder(connection.ConnectionString).Timeout);
        Assert.False(new NpgsqlConnectionStringBuilder(connection.ConnectionString).Pooling);
        policy.ClearPool(connection);
        Assert.Equal("SELECT \"MigrationId\" FROM \"platform\".\"PlatformMigrations\" ORDER BY \"MigrationId\"", policy.HistoryQuery(DatabaseStore.Platform));
        Assert.Equal("SELECT \"MigrationId\" FROM \"bff\".\"SessionMigrations\" ORDER BY \"MigrationId\"", policy.HistoryQuery(DatabaseStore.Sessions));
    }

    /// <summary>SQL Server restores the captured numeric timeout and disables connection retries only for readiness.</summary>
    [Fact]
    public void Given_SqlServerPolicy_When_CommandsAndBoundsAreApplied_Then_ExistingSemanticsRemain()
    {
        const string Provider = "SqlServer";
        const int Previous = -1;
        var policy = TestDatabaseProviders.Resolve(Provider).Connections;
        using var connection = new SqlConnection(TestDatabaseProviders.Resolve(Provider).DesignTimeConnection);
        using var command = connection.CreateCommand();
        policy.ReadLockTimeout(command);
        Assert.Equal("SELECT @@LOCK_TIMEOUT", command.CommandText);
        policy.SetLockTimeout(command);
        Assert.Equal("SET LOCK_TIMEOUT 5000", command.CommandText);
        policy.RestoreLockTimeout(command, Previous);
        Assert.Equal("SET LOCK_TIMEOUT -1", command.CommandText);
        Assert.Empty(command.Parameters);
        Assert.Null(policy.TransactionLockLimitSql);
        policy.Bound(connection, RequestSeconds, readiness: false);
        Assert.Equal(5, new SqlConnectionStringBuilder(connection.ConnectionString).ConnectTimeout);
        Assert.True(new SqlConnectionStringBuilder(connection.ConnectionString).Pooling);
        policy.Bound(connection, ReadinessSeconds, readiness: true);
        Assert.Equal(3, new SqlConnectionStringBuilder(connection.ConnectionString).ConnectTimeout);
        Assert.Equal(0, new SqlConnectionStringBuilder(connection.ConnectionString).ConnectRetryCount);
        Assert.False(new SqlConnectionStringBuilder(connection.ConnectionString).Pooling);
        policy.ClearPool(connection);
        Assert.Equal("SELECT [MigrationId] FROM [platform].[PlatformMigrations] ORDER BY [MigrationId]", policy.HistoryQuery(DatabaseStore.Platform));
        Assert.Equal("SELECT [MigrationId] FROM [bff].[SessionMigrations] ORDER BY [MigrationId]", policy.HistoryQuery(DatabaseStore.Sessions));
    }
}
