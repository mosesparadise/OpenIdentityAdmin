using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Denies provider reconnection once a pilot phase has bound its physical transaction.</summary>
internal sealed class PilotConnectionFence : DbConnectionInterceptor
{
    /// <summary>Allows only the phase's first synchronous open.</summary>
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        PilotStorageFence.Current?.RequireOpening(eventData.Context, connection);
        return result;
    }
    /// <summary>Allows only the phase's first asynchronous open.</summary>
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        PilotStorageFence.Current?.RequireOpening(eventData.Context, connection);
        return ValueTask.FromResult(result);
    }
}
