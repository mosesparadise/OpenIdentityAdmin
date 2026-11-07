using System.Data;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Commits only native Valid-to-Redeemed consumption in a separate child that never touches the parent's gate or replay cleanup.</summary>
internal static class PilotConsumption
{
    /// <summary>Retains the generation transaction while awaiting a separately durable, freshly bound native redemption.</summary>
    internal static async ValueTask RunAsync(IServiceScopeFactory factory, PilotContinuation continuation, CancellationToken cancellation)
    {
        var exchange = PilotExchange.Current ?? throw ProtocolStorageOwner.Unavailable();
        using var lifetime = new ProtocolStorageLifetime(exchange.Request.Clock, ProtocolStorageLifetime.TransactionLimit, cancellation);
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
            if (!await LockValidAsync(database, continuation, lifetime.Token) ||
                !await PilotAuthority.IsCurrentAsync(services, continuation, OpenIddictConstants.Statuses.Valid, lifetime.Token))
            {
                continuation.Context.Reject(OpenIddictConstants.Errors.InvalidGrant);
                return;
            }
            await services.GetRequiredService<OpenIddictServerHandlers.RedeemTokenEntry>().HandleAsync(continuation.Context);
            fence.RequireCurrent();
            if (continuation.Context.IsRejected)
            {
                return;
            }
            await transaction.CommitAsync(lifetime.Token);
            lifetime.RequireCurrent();
        }
        lifetime.RequireCurrent();
        continuation.CompleteConsumption();
    }

    /// <summary>Locks only an existing Valid code so native bulk revocation cannot change status between fresh validation and native tracked redemption.</summary>
    private static async Task<bool> LockValidAsync(PlatformDbContext database, PilotContinuation continuation, CancellationToken token)
    {
        continuation.RequireCurrent();
        var id = continuation.Principal.GetTokenId();
        return id is not null && await database.Set<OpenIddictEntityFrameworkCoreToken>()
            .Where(row => row.Id == id && row.Status == OpenIddictConstants.Statuses.Valid)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, row => row.Status), token) == 1;
    }
}
