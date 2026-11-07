using System.Data;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Retains a fresh legacy scope through a bounded gate transaction and actual native cleanup.</summary>
internal static class PilotProtocolTransaction
{
    private const string Unavailable = "Pilot protocol coordination is unavailable.";

    /// <summary>Serializes one validation phase without reusing a request's tracked native entities or renewing its cancellation.</summary>
    internal static async ValueTask RunAsync(IServiceScopeFactory factory, TimeProvider clock,
        Func<IServiceProvider, CancellationToken, ValueTask> action, CancellationToken cancellation)
    {
        using var lifetime = new ProtocolStorageLifetime(clock, ProtocolStorageLifetime.TransactionLimit, cancellation);
        await using (var scope = factory.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            services.GetService<ProtocolStorageSelection>()?.Select(ProtocolStorageMode.LegacyPilot);
            var database = services.GetRequiredService<PlatformDbContext>();
            await using var connection = new CeremonyConnection(database);
            await connection.OpenAsync(lifetime.Token);
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, lifetime.Token);
            await AcquireAsync(database, lifetime.Token);
            lifetime.RequireCurrent();
            await action(services, lifetime.Token);
            lifetime.RequireCurrent();
            await transaction.CommitAsync(lifetime.Token);
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
