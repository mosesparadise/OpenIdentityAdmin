using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Fences every EF native command, including handlers that omit cancellation or use synchronous store operations.</summary>
internal sealed class PilotCommandFence : DbCommandInterceptor
{
    /// <summary>Requires the exact live phase or denies all post-generation storage access.</summary>
    private static void Require(DbCommand command, CommandEventData data)
    {
        if (PilotStorageFence.Current is { } fence)
        {
            fence.RequireCommand(data.Context, command);
        }
        else if (PilotExchange.Current is { StorageClosed: true } exchange)
        {
            exchange.Request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Fences synchronous queries before execution.</summary>
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Require(command, eventData);
        return result;
    }
    /// <summary>Fences asynchronous queries before execution.</summary>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Require(command, eventData);
        return ValueTask.FromResult(result);
    }
    /// <summary>Fences synchronous mutations before execution.</summary>
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Require(command, eventData);
        return result;
    }
    /// <summary>Fences asynchronous mutations before execution.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Require(command, eventData);
        return ValueTask.FromResult(result);
    }
    /// <summary>Fences synchronous scalar commands before execution.</summary>
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Require(command, eventData);
        return result;
    }
    /// <summary>Fences asynchronous scalar commands before execution.</summary>
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Require(command, eventData);
        return ValueTask.FromResult(result);
    }
}
