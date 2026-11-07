using System.Data;
using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Binds native commands to one explicit context, physical connection and transaction without allowing reconnect or autocommit.</summary>
internal sealed class PilotStorageFence : IDisposable
{
    private static readonly AsyncLocal<PilotStorageFence?> Ambient = new();
    private readonly PilotStorageFence? parent;
    private readonly PlatformDbContext database;
    private readonly ProtocolStorageLifetime lifetime;
    private readonly PilotExchange? exchange;
    private readonly DbConnection connection;
    private IDbContextTransaction? transaction;
    private DbTransaction? native;
    private bool closed;
    internal static PilotStorageFence? Current => Ambient.Value;

    /// <summary>Enters a single fresh native phase before its initial connection opens.</summary>
    internal PilotStorageFence(PlatformDbContext database, ProtocolStorageLifetime lifetime)
    {
        this.database = database;
        this.lifetime = lifetime;
        exchange = PilotExchange.Current;
        connection = database.Database.GetDbConnection();
        parent = Ambient.Value;
        parent?.RequireCurrent();
        Ambient.Value = this;
    }

    /// <summary>Binds exactly once, after explicit native transaction creation and before any manager command.</summary>
    internal void Bind(IDbContextTransaction value)
    {
        if (transaction is not null || closed)
        {
            throw Reject();
        }
        transaction = value;
        native = value.GetDbTransaction();
        RequireCurrent();
    }

    /// <summary>Checks both the original request and strict transaction budget, including a held parent during durable consumption.</summary>
    internal void RequireCurrent()
    {
        exchange?.Request.RequireCurrent();
        lifetime.RequireCurrent();
        parent?.RequireCurrent();
        if (closed || exchange?.StorageClosed == true || transaction is null || !ReferenceEquals(database.Database.CurrentTransaction, transaction) ||
            !ReferenceEquals(transaction.GetDbTransaction(), native) || !ReferenceEquals(native!.Connection, connection) || connection.State != ConnectionState.Open)
        {
            throw Reject();
        }
    }

    /// <summary>Rejects commands from another scope, a replacement transaction or an autocommit continuation.</summary>
    internal void RequireCommand(DbContext? context, DbCommand command)
    {
        RequireCurrent();
        if (!ReferenceEquals(context, database) || !ReferenceEquals(command.Connection, connection) || !ReferenceEquals(command.Transaction, native))
        {
            throw Reject();
        }
    }

    /// <summary>Allows only the first opening before binding; a lost gate cannot open a replacement connection.</summary>
    internal void RequireOpening(DbContext? context, DbConnection value)
    {
        exchange?.Request.RequireCurrent();
        lifetime.RequireCurrent();
        if (closed || transaction is not null || !ReferenceEquals(context, database) || !ReferenceEquals(value, connection))
        {
            throw Reject();
        }
    }

    /// <summary>Checks the exact transaction immediately before a native commit.</summary>
    internal void RequireCommit(DbContext? context, DbTransaction value)
    {
        RequireCurrent();
        if (!ReferenceEquals(context, database) || !ReferenceEquals(value, native))
        {
            throw Reject();
        }
    }

    /// <summary>Irreversibly closes this phase and restores only its enclosing async flow.</summary>
    public void Dispose()
    {
        closed = true;
        Ambient.Value = parent;
    }

    /// <summary>Latches unsafe storage attempts even when a native best-effort handler catches their exception.</summary>
    private InvalidOperationException Reject()
    {
        exchange?.Request.Fail();
        return ProtocolStorageOwner.Unavailable();
    }
}
