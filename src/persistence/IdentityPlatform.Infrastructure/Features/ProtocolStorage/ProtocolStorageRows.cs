using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Refreshes exact native rows under owned execution before persisted authority is compared.</summary>
internal static class ProtocolStorageRows
{
    /// <summary>Prevents tracked identity resolution from concealing persisted drift; a deleted row never qualifies.</summary>
    internal static ValueTask<bool> ReloadAsync(ProtocolStorageScope scope, object row, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var entry = scope.Owner.Database.Entry(row);
        await entry.ReloadAsync(current);
        return entry.State != EntityState.Detached;
    }, token);
}
