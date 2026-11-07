using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Finds a bounded keyset page of runnable candidates without treating queue visibility as processing authority.</summary>
internal static class CatalogueDeletionQueue
{
    private const int PollLimit = 16;

    /// <summary>Reads one keyset page of runnable work in short rollback-only ownership; each candidate still needs fresh serialized batch checks.</summary>
    internal static async Task<Guid[]> ReadAsync(IServiceScopeFactory factory, TimeProvider clock, Guid? after, CancellationToken token)
    {
        var request = new ProtocolStorageRequest(clock, token);
        try
        {
            return await request.RunAsync(factory, (scope, current) => OwnedAsync(scope, after, current));
        }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Materializes only identifiers and disposes the actual read transaction before another request can process them.</summary>
    private static async ValueTask<Guid[]> OwnedAsync(ProtocolStorageScope scope, Guid? after, CancellationToken token)
    {
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        return await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<CatalogueDeletion>().AsNoTracking()
            .Where(row => row.State == CatalogueDeletionState.Queued && (after == null || row.Id.CompareTo(after.Value) > 0))
            .OrderBy(row => row.Id).Take(PollLimit).Select(row => row.Id).ToArrayAsync(current), token);
    }
}
