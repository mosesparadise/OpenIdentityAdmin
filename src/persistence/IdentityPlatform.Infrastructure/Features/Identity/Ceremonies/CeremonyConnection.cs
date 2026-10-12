using System.Data;
using System.Globalization;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Owns explicit connection, command and session limits, including cleanup discovery outside a transaction.</summary>
internal sealed class CeremonyConnection(PlatformDbContext database) : IAsyncDisposable
{
    private const string SqlRead = "SELECT @@LOCK_TIMEOUT";
    private const string SqlSet = "SET LOCK_TIMEOUT 5000";
    private const string SqlRestore = "SET LOCK_TIMEOUT ";
    private const string PostgresRead = "SELECT current_setting('lock_timeout')";
    private const string PostgresSet = "SET lock_timeout = '5s'";
    private const string PostgresRestore = "SELECT set_config('lock_timeout', @previous, false)";
    private const string PreviousParameter = "previous";
    private readonly int? commandTimeout = database.Database.GetCommandTimeout();
    private object? lockTimeout;
    private bool opened;
    private bool disposed;

    /// <summary>Bounds all commands and provider lock waits before transaction creation or candidate discovery.</summary>
    internal async Task OpenAsync(CancellationToken token)
    {
        await database.Database.OpenConnectionAsync(token);
        opened = true;
        database.Database.SetCommandTimeout(AdministratorPolicy.OperationSeconds);
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandTimeout = AdministratorPolicy.OperationSeconds;
        command.CommandText = database.Database.IsSqlServer() ? SqlRead : PostgresRead;
        lockTimeout = await command.ExecuteScalarAsync(token);
        command.CommandText = database.Database.IsSqlServer() ? SqlSet : PostgresSet;
        await command.ExecuteNonQueryAsync(token);
    }

    /// <summary>Restores session settings; failed restoration evicts the physical connection instead of returning unsafe pooled state.</summary>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        try
        {
            await RestoreAsync();
        }
        catch
        {
            await DiscardAsync();
            throw;
        }
        finally
        {
            database.Database.SetCommandTimeout(commandTimeout);
            if (opened)
            {
                await database.Database.CloseConnectionAsync();
            }
        }
    }

    /// <summary>Restores the exact captured provider value under an independent deadline after rollback or commit.</summary>
    private async Task RestoreAsync()
    {
        var connection = database.Database.GetDbConnection();
        if (!opened || lockTimeout is null || connection.State != ConnectionState.Open)
        {
            return;
        }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(AdministratorPolicy.OperationSeconds));
        await using var command = connection.CreateCommand();
        command.CommandTimeout = AdministratorPolicy.OperationSeconds;
        if (database.Database.IsSqlServer())
        {
            command.CommandText = SqlRestore + Convert.ToInt32(lockTimeout, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            command.CommandText = PostgresRestore;
            var previous = command.CreateParameter();
            previous.ParameterName = PreviousParameter;
            previous.Value = lockTimeout;
            command.Parameters.Add(previous);
        }
        await command.ExecuteNonQueryAsync(deadline.Token);
    }

    /// <summary>Marks the failed physical session for discard and closes it even when EF did not own the original open.</summary>
    private async Task DiscardAsync()
    {
        var connection = database.Database.GetDbConnection();
        try
        {
            if (connection is SqlConnection sql)
            {
                SqlConnection.ClearPool(sql);
            }
            if (connection is NpgsqlConnection postgres)
            {
                NpgsqlConnection.ClearPool(postgres);
            }
        }
        finally { await connection.CloseAsync(); }
    }
}
