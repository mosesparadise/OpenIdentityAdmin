using System.Security.Claims;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Verifies strict persisted/principal expiry and ambiguous native commit without rolling back already-consumed state.</summary>
internal static class NativeConsumptionBoundaries
{
    private static readonly int[] ExpiryOffsets = [-1, 0, 1];
    private const int FutureSeconds = 1;

    /// <summary>Runs equality and post-wait proofs under each owning store/provider with real commit completion.</summary>
    internal static async Task VerifyAsync(IServiceProvider root, DatabaseStore store, CancellationToken token)
    {
        var type = store == DatabaseStore.Sessions ? OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken :
            OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode;
        var clock = root.GetRequiredService<NativeConsumptionClock>();
        try
        {
            var now = clock.Freeze();
            foreach (var offset in ExpiryOffsets)
            {
                await VerifyExpiryAsync(root, type, now.AddSeconds(offset), offset > 0, false, token);
                await VerifyExpiryAsync(root, type, now.AddSeconds(offset), offset > 0, true, token);
            }
            await VerifyLateCommitAsync(root, type, clock, token);
            clock.Reset();
            await VerifyUnknownCommitAsync(root, type, token);
        }
        finally { clock.Reset(); root.GetRequiredService<NativeConsumptionCommit>().AfterCommit = null; }
    }

    /// <summary>Independently varies the row or protected principal expiry; the earlier bound always governs consumption.</summary>
    private static async Task VerifyExpiryAsync(IServiceProvider root, string type, DateTimeOffset expiration, bool accepted, bool persisted, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(root, type, token);
        if (persisted)
        {
            await SetExpirationAsync(root, principal, expiration, token);
        }
        else
        {
            principal.SetExpirationDate(expiration);
        }
        Assert.Equal(accepted, await NativeConsumptionConformance.ConsumeAsync(root, principal, token));
        await NativeConsumptionConformance.RequireStatusAsync(root, principal, accepted ? OpenIddictConstants.Statuses.Redeemed : OpenIddictConstants.Statuses.Valid, token);
    }

    /// <summary>Changes the native row only under its owning lock using native manager population/update semantics.</summary>
    private static Task<bool> SetExpirationAsync(IServiceProvider root, ClaimsPrincipal principal, DateTimeOffset expiration, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            if (principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken))
            {
                await AdministratorProtocolLocks.AcquireClientStateAsync(scope.Owner, cancellation);
            }
            else
            {
                await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            }
            var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
            var entry = await manager.FindByIdAsync(principal.GetTokenId()!, cancellation);
            var descriptor = new OpenIddictTokenDescriptor();
            await manager.PopulateAsync(descriptor, entry!, cancellation);
            descriptor.ExpirationDate = expiration;
            await manager.UpdateAsync(entry!, descriptor, cancellation);
            await transaction.CommitAsync(cancellation);
            return true;
        });

    /// <summary>Expiry during actual commit denies continuation while the earlier durable redemption remains observable.</summary>
    private static async Task VerifyLateCommitAsync(IServiceProvider root, string type, NativeConsumptionClock clock, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(root, type, token);
        var expiration = clock.GetUtcNow().AddSeconds(FutureSeconds);
        await SetExpirationAsync(root, principal, expiration, token);
        root.GetRequiredService<NativeConsumptionCommit>().AfterCommit = () => clock.Set(expiration);
        Assert.False(await NativeConsumptionConformance.ConsumeAsync(root, principal, token));
        await NativeConsumptionConformance.RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Redeemed, token);
    }

    /// <summary>A lost native commit acknowledgement faults the original request; a fresh attempt cannot redeem again.</summary>
    private static async Task VerifyUnknownCommitAsync(IServiceProvider root, string type, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(root, type, token);
        var expected = new ArithmeticException();
        var commits = 0;
        root.GetRequiredService<NativeConsumptionCommit>().AfterCommit = () => { commits++; throw expected; };
        Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => NativeConsumptionConformance.ConsumeAsync(root, principal, token)));
        Assert.Equal(1, commits);
        await NativeConsumptionConformance.RequireStatusAsync(root, principal, OpenIddictConstants.Statuses.Redeemed, token);
        Assert.False(await NativeConsumptionConformance.ConsumeAsync(root, principal, token));
    }
}
