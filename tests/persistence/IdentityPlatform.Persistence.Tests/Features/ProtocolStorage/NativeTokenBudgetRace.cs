using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Observes actual provider lock contention while two independent instances compete for the final native exchange capacity.</summary>
internal static class NativeTokenBudgetRace
{
    private const int ExchangeRows = 2;

    /// <summary>Requires the peer database session to be blocked by the exact reserved transaction before releasing the winning native creation.</summary>
    internal static async Task VerifyAsync(IServiceProvider first, IServiceProvider second, NativeLockObservation observer, CancellationToken token)
    {
        var reserved = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var winner = CreateAsync(first, reserved, release.Task, null, token);
        Task<bool>? peer = null;
        Task? waiting = null;
        using var observation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var observed = false;
        try
        {
            await RequireSignalAsync(reserved.Task, winner, token);
            var session = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            peer = CreateAsync(second, null, Task.CompletedTask, session, token);
            await RequireSignalAsync(session.Task, peer, token);
            waiting = observer.WaitAsync(await session.Task, await reserved.Task, observation.Token);
            if (await Task.WhenAny(waiting, peer).WaitAsync(token) == waiting)
            {
                await waiting;
                observed = true;
            }
        }
        finally
        {
            release.TrySetResult();
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
                finally { await Task.WhenAll(winner, peer ?? Task.CompletedTask); }
            }
        }
        Assert.True(observed);
        Assert.True(await winner);
        Assert.False(await peer!);
    }

    /// <summary>Fails immediately if a competitor exits before reaching its intended database observation boundary.</summary>
    private static async Task RequireSignalAsync(Task signal, Task operation, CancellationToken token)
    {
        await Task.WhenAny(signal, operation).WaitAsync(token);
        if (!signal.IsCompleted)
        {
            await operation;
            throw new InvalidOperationException();
        }
    }

    /// <summary>Retains the profile lock across exact count, observed contention, actual native manager writes and final reconciliation.</summary>
    private static Task<bool> CreateAsync(IServiceProvider root, TaskCompletionSource<int>? reserved, Task release,
        TaskCompletionSource<int>? beforeLock, CancellationToken token) => NativeTokenBudgetFixture.WriteAsync(root, async (scope, cancellation) =>
        {
            var application = await NativeTokenBudgetFixture.ApplicationAsync(scope, cancellation);
            var budget = await AdministratorTokenBudget.ReserveLockedAsync(scope.Owner, application, AdministratorTokenCreation.TokenExchange, TimeSpan.Zero, cancellation);
            Assert.NotNull(budget);
            if (!budget.HasCapacity)
            {
                return (false, await budget.CanCommitCleanupAsync(cancellation));
            }
            reserved?.SetResult(NativeLockObservation.Session(scope.Owner.Database.Database.GetDbConnection()));
            await release.WaitAsync(cancellation);
            await NativeTokenBudgetFixture.CreateTokensAsync(scope, application, ExchangeRows, OpenIddictConstants.TokenTypeIdentifiers.AccessToken, cancellation);
            var current = await budget.IsCurrentAsync(cancellation);
            return (current, current);
        }, token, scope => beforeLock?.SetResult(NativeLockObservation.Session(scope.Owner.Database.Database.GetDbConnection())));
}
