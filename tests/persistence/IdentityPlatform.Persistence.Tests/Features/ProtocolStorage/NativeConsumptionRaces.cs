using System.Security.Claims;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Uses independent providers and original requests to exercise serialized replay and native bulk-revocation ordering.</summary>
internal static class NativeConsumptionRaces
{
    /// <summary>Both stale prevalidated contenders reach independent native contexts; only one may consume the durable token.</summary>
    internal static async Task VerifyAsync(IServiceProvider first, IServiceProvider second, DatabaseStore store, NativeLockObservation observer, CancellationToken token)
    {
        var type = store == DatabaseStore.Sessions ? OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken :
            OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode;
        var principal = await NativeConsumptionSeed.TokenAsync(first, type, token);
        var outcomes = await Task.WhenAll(NativeConsumptionConformance.ConsumeAsync(first, principal, token),
            NativeConsumptionConformance.ConsumeAsync(second, principal, token));
        Assert.Single(outcomes, accepted => accepted);
        Assert.Single(outcomes, accepted => !accepted);
        if (store == DatabaseStore.Platform)
        {
            await VerifyRevocationFirstAsync(first, second, token);
            await VerifyRedemptionFirstAsync(first, second, observer, token);
            await VerifyChainAsync(first, second, token);
        }
    }

    /// <summary>Proves the profile lock prevents bulk revocation from being overwritten by a paused, already-materialized native update.</summary>
    private static async Task VerifyRedemptionFirstAsync(IServiceProvider first, IServiceProvider second, NativeLockObservation observer, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(first, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token);
        var gate = first.GetRequiredService<NativeRedemptionBarrier>().Arm(principal.GetTokenId()!);
        var consume = NativeConsumptionConformance.ConsumeAsync(first, principal, token);
        Task? revoke = null;
        Task? waiting = null;
        using var observation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var observed = false;
        try
        {
            await Task.WhenAny(gate.Reached.Task, consume).WaitAsync(token);
            if (!gate.Reached.Task.IsCompleted)
            {
                await consume;
                throw new InvalidOperationException();
            }
            var session = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            revoke = RevokeAsync(second, principal, session, token);
            await Task.WhenAny(session.Task, revoke).WaitAsync(token);
            if (!session.Task.IsCompleted)
            {
                await revoke;
                throw new InvalidOperationException();
            }
            waiting = observer.WaitAsync(await session.Task, await gate.Reached.Task, observation.Token);
            if (await Task.WhenAny(waiting, revoke).WaitAsync(token) == waiting)
            {
                await waiting;
                observed = true;
            }
        }
        finally
        {
            gate.Release.TrySetResult();
            try
            {
                await observation.CancelAsync();
            }
            finally
            {
                try
                {
                    if (waiting is not null)
                    {
                        try
                        {
                            await waiting;
                        }
                        catch (OperationCanceledException) when (observation.IsCancellationRequested) { }
                    }
                }
                finally { await Task.WhenAll(revoke ?? Task.CompletedTask, consume); }
            }
        }
        await NativeConsumptionConformance.RequireStatusAsync(second, principal, OpenIddictConstants.Statuses.Revoked, token);
        Assert.True(observed);
    }

    /// <summary>Announces the peer's exact connection before acquiring the same profile lock and performing a native bulk revocation.</summary>
    private static Task<bool> RevokeAsync(IServiceProvider root, ClaimsPrincipal principal, TaskCompletionSource<int> session, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            session.SetResult(NativeLockObservation.Session(scope.Owner.Database.Database.GetDbConnection()));
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            Assert.Equal(1, await scope.Services.GetRequiredService<IOpenIddictTokenManager>().RevokeByAuthorizationIdAsync(principal.GetAuthorizationId()!, cancellation));
            await transaction.CommitAsync(cancellation);
            return true;
        });

    /// <summary>A bulk revocation committing first cannot be overwritten by redemption of an earlier validated principal.</summary>
    private static async Task VerifyRevocationFirstAsync(IServiceProvider first, IServiceProvider second, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(first, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token);
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var revoke = new ProtocolStorageRequest(first.GetRequiredService<TimeProvider>(), token).RunAsync(first.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            Assert.Equal(1, await scope.Services.GetRequiredService<IOpenIddictTokenManager>().RevokeByAuthorizationIdAsync(principal.GetAuthorizationId()!, cancellation));
            locked.SetResult();
            await release.Task.WaitAsync(cancellation);
            await transaction.CommitAsync(cancellation);
            return true;
        });
        Task<bool>? consume = null;
        try
        {
            await Task.WhenAny(locked.Task, revoke).WaitAsync(token);
            if (!locked.Task.IsCompleted)
            {
                await revoke;
                throw new InvalidOperationException();
            }
            consume = NativeConsumptionConformance.ConsumeAsync(second, principal, token);
        }
        finally { release.TrySetResult(); await Task.WhenAll(revoke, consume ?? Task.CompletedTask); }
        Assert.False(await consume!);
        await NativeConsumptionConformance.RequireStatusAsync(second, principal, OpenIddictConstants.Statuses.Revoked, token);
    }

    /// <summary>Replay revokes a different live token on the same authorization while preserving unrelated authorization state.</summary>
    private static async Task VerifyChainAsync(IServiceProvider first, IServiceProvider second, CancellationToken token)
    {
        var principal = await NativeConsumptionSeed.TokenAsync(first, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token);
        var linked = new ClaimsPrincipal(new ClaimsIdentity(principal.Claims));
        await new ProtocolStorageRequest(first.GetRequiredService<TimeProvider>(), token).RunAsync(first.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            await NativeConsumptionDispatch.CreateAsync(scope, linked);
            await transaction.CommitAsync(cancellation);
            return true;
        });
        var unrelated = await NativeConsumptionSeed.TokenAsync(first, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token);
        Assert.True(await NativeConsumptionConformance.ConsumeAsync(first, principal, token));
        Assert.False(await new ProtocolStorageRequest(second.GetRequiredService<TimeProvider>(), token).RunAsync(second.GetRequiredService<IServiceScopeFactory>(),
            (scope, _) => NativeConsumptionDispatch.ValidateAsync(scope, principal)));
        await NativeConsumptionConformance.RequireStatusAsync(first, linked, OpenIddictConstants.Statuses.Revoked, token);
        await NativeConsumptionConformance.RequireStatusAsync(first, unrelated, OpenIddictConstants.Statuses.Valid, token);
    }
}
