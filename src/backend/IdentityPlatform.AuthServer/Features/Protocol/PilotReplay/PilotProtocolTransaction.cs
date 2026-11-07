using System.Data;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Retains a fresh legacy scope through a bounded gate transaction and actual native cleanup.</summary>
internal static class PilotProtocolTransaction
{
    private const string Unavailable = "Pilot protocol coordination is unavailable.";

    /// <summary>Serializes one validation phase without reusing a request's tracked native entities or renewing its cancellation.</summary>
    internal static async ValueTask RunAsync(IServiceScopeFactory factory, TimeProvider clock,
        Func<IServiceProvider, CancellationToken, ValueTask> action, CancellationToken cancellation)
    {
        await RunPhaseAsync(factory, clock, async (services, token) => { await action(services, token); return true; }, cancellation);
    }

    /// <summary>Commits only an explicitly completed phase; rejected generation rolls back without reversing a durable consumption child.</summary>
    internal static async ValueTask RunPhaseAsync(IServiceScopeFactory factory, TimeProvider clock,
        Func<IServiceProvider, CancellationToken, ValueTask<bool>> action, CancellationToken cancellation)
    {
        using var lifetime = new ProtocolStorageLifetime(clock, ProtocolStorageLifetime.TransactionLimit, cancellation);
        await using (var scope = factory.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            services.GetService<ProtocolStorageSelection>()?.Select(ProtocolStorageMode.LegacyPilot);
            var database = services.GetRequiredService<PlatformDbContext>();
            using var fence = new PilotStorageFence(database, lifetime);
            await using var connection = new CeremonyConnection(database);
            await connection.OpenAsync(lifetime.Token);
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, lifetime.Token);
            fence.Bind(transaction);
            await AcquireAsync(database, lifetime.Token);
            lifetime.RequireCurrent();
            var commit = await action(services, lifetime.Token);
            lifetime.RequireCurrent();
            if (commit)
            {
                await transaction.CommitAsync(lifetime.Token);
            }
            lifetime.RequireCurrent();
        }
        lifetime.RequireCurrent();
    }

    /// <summary>A conditional no-op update locks exactly the migration-owned row; absent state is never created by a request.</summary>
    internal static async Task AcquireAsync(PlatformDbContext database, CancellationToken cancellation)
    {
        if (database.Database.CurrentTransaction is null || await database.Set<PilotProtocolGate>()
            .Where(row => row.Id == PilotProtocolGate.SingletonId).ExecuteUpdateAsync(update => update.SetProperty(row => row.Id, row => row.Id), cancellation) != 1)
        {
            throw new InvalidOperationException(Unavailable);
        }
    }
}
