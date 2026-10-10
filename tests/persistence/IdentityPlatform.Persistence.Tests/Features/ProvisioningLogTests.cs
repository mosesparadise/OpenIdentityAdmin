using System.Data.Common;
using System.Security.Cryptography;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.MsSql;

namespace IdentityPlatform.Persistence.Tests.Features;

public sealed class ProvisioningLogTests
{
    [Theory]
    [InlineData(DatabaseProvider.PostgreSql, Traits = ["Provider", "PostgreSql"])]
    [InlineData(DatabaseProvider.SqlServer, Traits = ["Provider", "SqlServer"])]
    public async Task Failed_local_provisioning_does_not_put_passwords_in_database_logs(DatabaseProvider provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var token = deadline.Token;
        await using var postgres = new PostgreSqlBuilder(
            "postgres:18@sha256:74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336")
            .WithCommand("-c", "log_statement=all", "-c", "log_transaction_sample_rate=1").Build();
        await using var sql = new MsSqlBuilder(
            "mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090").Build();
        if (provider == DatabaseProvider.PostgreSql) await postgres.StartAsync(token);
        else await sql.StartAsync(token);
        var connection = provider == DatabaseProvider.PostgreSql ? postgres.GetConnectionString() : sql.GetConnectionString();
        var options = new DbContextOptionsBuilder<PlatformDbContext>();
        PersistenceRegistration.ConfigureDatabase(options, provider, connection, "PlatformMigrations");
        await using var database = new PlatformDbContext(options.Options);
        var password = "aA1" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        if (provider == DatabaseProvider.PostgreSql)
        {
            await database.Database.OpenConnectionAsync(token);
            await database.Database.ExecuteSqlRawAsync("SET log_statement = 'all'", token);
        }
        // No schema/history exists: role DDL is followed by a deliberate grant failure.
        await Assert.ThrowsAnyAsync<DbException>(() => LocalRuntimeRoleProvisioner.ProvisionAsync(
            database, RuntimeStore.Platform, password, token));
        const string sqlControl = "Synthetic provisioning log capture control";
        if (provider == DatabaseProvider.SqlServer)
        {
            // Force an error-log entry after the failed command, without including its
            // text/credentials. Absence is meaningful only once capture reaches here.
            await Assert.ThrowsAnyAsync<DbException>(() => database.Database.ExecuteSqlRawAsync(
                "RAISERROR ('Synthetic provisioning log capture control', 16, 1) WITH LOG", token));
        }
        var (stdout, stderr) = provider == DatabaseProvider.PostgreSql
            ? await postgres.GetLogsAsync(ct: token) : await sql.GetLogsAsync(ct: token);
        var control = provider == DatabaseProvider.PostgreSql ? "schema \"platform\" does not exist" : sqlControl;
        {
            using var logsDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (!(stdout + stderr).Contains(control, StringComparison.Ordinal))
            {
                await timer.WaitForNextTickAsync(logsDeadline.Token);
                (stdout, stderr) = provider == DatabaseProvider.PostgreSql
                    ? await postgres.GetLogsAsync(ct: logsDeadline.Token)
                    : await sql.GetLogsAsync(ct: logsDeadline.Token);
            }
        }
        Assert.False((stdout + stderr).Contains(password, StringComparison.Ordinal),
            "The database logged a password from the failed bootstrap command.");
    }
}
