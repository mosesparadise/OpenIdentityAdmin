using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Finds a bounded fair set of runnable candidates without treating queue visibility as processing authority.</summary>
internal static class GrantJobQueue
{
    private const int PollLimit = 16;

    /// <summary>Reads oldest observed runnable work in short rollback-only ownership; each candidate still needs fresh serialized batch checks.</summary>
    internal static async Task<Guid[]> ReadAsync(IServiceScopeFactory factory, TimeProvider clock, CancellationToken token)
    {
        var request = new ProtocolStorageRequest(clock, token);
        try
        {
            return await request.RunAsync(factory, OwnedAsync);
        }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Materializes only identifiers and disposes the actual read transaction before another request can process them.</summary>
    private static async ValueTask<Guid[]> OwnedAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        return await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<ApplicationGrantJob>().AsNoTracking()
            .Where(row => row.State == GrantJobState.Queued || row.State == GrantJobState.Processing)
            .OrderBy(row => row.ObservedAt).ThenBy(row => row.Id).Take(PollLimit).Select(row => row.Id).ToArrayAsync(current), token);
    }
}
