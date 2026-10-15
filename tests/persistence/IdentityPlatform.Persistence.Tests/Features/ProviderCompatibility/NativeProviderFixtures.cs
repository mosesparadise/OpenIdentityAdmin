using System.Data.Common;
using System.Security.Cryptography;
using IdentityPlatform.Database;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Supplies the two shipped engine fixtures; reusable assertions contain no engine switch or exception-code fallback.</summary>
internal static class NativeProviderFixtures
{
    private const string Postgres = "PostgreSql";
    private const string PasswordClasses = "aA1";
    private const int RandomCharacters = 40;
    private const string PostgresRead = "SELECT current_setting('lock_timeout')";
    private const string SqlRead = "SELECT @@LOCK_TIMEOUT";
    private const string PostgresBound = "5s";
    private const string SqlBound = "5000";
    private const string PostgresDenied = "42501";
    private static readonly int[] SqlDenied = [229, 262, 2760];

    /// <summary>Creates disposable credentials and independently expected native permission/lock outcomes.</summary>
    internal static NativeProviderFixture Create(string name, string administratorConnection)
    {
        var provider = TestDatabaseProviders.Resolve(name);
        var password = PasswordClasses + RandomNumberGenerator.GetHexString(RandomCharacters);
        return new NativeProviderFixture(provider,
            store => RuntimeConnection(name, administratorConnection, store, password),
            token => ProvisionAsync(provider, administratorConnection, password, token),
            exception => RequireDenied(name, exception), exception => RequireLockTimeout(name, exception),
            name == Postgres ? PostgresRead : SqlRead, name == Postgres ? PostgresBound : SqlBound);
    }

    /// <summary>Uses fixture administrator credentials only to provision the requested runtime principals.</summary>
    private static async Task ProvisionAsync(IRelationalDatabaseProvider provider, string connection, string password, CancellationToken token)
    {
        var capability = provider.LocalProvisioning!;
        await capability.ProvisionAsync(connection, DatabaseStore.Platform, password, token);
        await capability.ProvisionAsync(connection, DatabaseStore.Sessions, password, token);
    }

    /// <summary>Constructs independently supplied runtime credentials while preserving the fixture's physical target and transport.</summary>
    private static string RuntimeConnection(string name, string administrator, DatabaseStore store, string password)
    {
        var role = LocalRolePolicy.Role(store);
        return name == Postgres
            ? new NpgsqlConnectionStringBuilder(administrator) { Username = role, Password = password }.ConnectionString
            : new SqlConnectionStringBuilder(administrator) { UserID = role, Password = password, IntegratedSecurity = false }.ConnectionString;
    }

    /// <summary>Accepts only documented native permission-denied codes, never unrelated failures or SQL1088 alone.</summary>
    private static void RequireDenied(string name, DbException exception)
    {
        if (name == Postgres)
        {
            Assert.Equal(PostgresDenied, Assert.IsType<PostgresException>(exception).SqlState);
        }
        else
        {
            Assert.Contains(Assert.IsType<SqlException>(exception).Number, SqlDenied);
        }
    }

    /// <summary>Requires native lock timeout rather than accepting cancellation, command timeout or unrelated errors.</summary>
    private static void RequireLockTimeout(string name, DbException exception)
    {
        const string PostgresLockTimeout = "55P03";
        const int SqlLockTimeout = 1222;
        if (name == Postgres)
        {
            Assert.Equal(PostgresLockTimeout, Assert.IsType<PostgresException>(exception).SqlState);
        }
        else
        {
            Assert.Equal(SqlLockTimeout, Assert.IsType<SqlException>(exception).Number);
        }
    }

}
