using IdentityPlatform.Database;
using System.Data;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Owns explicit connection, command and session limits, including cleanup discovery outside a transaction.</summary>
internal sealed class CeremonyConnection(PlatformDbContext database) : IAsyncDisposable
{
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
        database.GetDatabaseProvider().Connections.ReadLockTimeout(command);
        lockTimeout = await command.ExecuteScalarAsync(token);
        database.GetDatabaseProvider().Connections.SetLockTimeout(command);
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
        database.GetDatabaseProvider().Connections.RestoreLockTimeout(command, lockTimeout);
        await command.ExecuteNonQueryAsync(deadline.Token);
    }

    /// <summary>Marks the failed physical session for discard and closes it even when EF did not own the original open.</summary>
    private async Task DiscardAsync()
    {
        var connection = database.Database.GetDbConnection();
        try
        {
            database.GetDatabaseProvider().Connections.ClearPool(connection);
        }
        finally { await connection.CloseAsync(); }
    }
}
