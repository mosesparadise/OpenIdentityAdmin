using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Proves swallowed native update cancellation and forbidden maintenance cannot reach an owner commit.</summary>
internal static class NativeOwnershipFailures
{
    /// <summary>Runs both actual native manager catch paths and validates independent committed state after rollback.</summary>
    internal static async Task VerifyAsync(IServiceProvider root, CancellationToken token)
    {
        await CancelUpdateAsync(root, false, token);
        await NativeOwnershipConformance.RequireStateAsync(root, true, token);
        await CancelUpdateAsync(root, true, token);
        await NativeOwnershipConformance.RequireStateAsync(root, true, token);
        await RejectPruneAsync(root, token);
        await RejectOutstandingReaderAsync(root, token);
        await RejectReplacedTransactionAsync(root, token);
    }

    /// <summary>Cancels inside real EF SaveChanges and requires the installed manager's false result plus owner commit rejection.</summary>
    private static async Task CancelUpdateAsync(IServiceProvider root, bool revoke, CancellationToken token)
    {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(token);
        await using var scope = await NativeOwnershipConformance.OpenAsync(root, request.Token);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        List<object> rows = [];
        await foreach (var row in manager.FindBySubjectAsync(NativeOwnershipConformance.Subject, token))
        {
            rows.Add(row);
        }
        var target = Assert.Single(rows);
        var interceptor = root.GetRequiredService<CancelNativeUpdate>();
        interceptor.Arm(request);
        try
        {
            Assert.False(revoke ? await manager.TryRevokeAsync(target, token) : await manager.TryRedeemAsync(target, token));
            Assert.True(request.IsCancellationRequested);
            Assert.Equal(1, interceptor.Calls);
            Assert.True(interceptor.ObservedLinkedCancellation);
            Assert.NotNull(interceptor.Cancellation);
            Assert.Equal(interceptor.ReceivedToken, interceptor.Cancellation.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync(token));
        }
        finally { interceptor.Request = null; }
    }

    /// <summary>Rejecting either broad native Prune method is terminal even when the caller catches the exception.</summary>
    private static async Task RejectPruneAsync(IServiceProvider root, CancellationToken token)
    {
        await using var scope = await NativeOwnershipConformance.OpenAsync(root, token);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var tokens = scope.Services.GetRequiredService<IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>>();
        var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationStore<OpenIddictEntityFrameworkCoreAuthorization>>();
        await Assert.ThrowsAsync<NotSupportedException>(() => tokens.PruneAsync(DateTimeOffset.UtcNow, token).AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => authorizations.PruneAsync(DateTimeOffset.UtcNow, token).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync(token));
    }

    /// <summary>A reader paused between yields excludes commit until its real native enumerator has been disposed.</summary>
    private static async Task RejectOutstandingReaderAsync(IServiceProvider root, CancellationToken token)
    {
        await using var scope = await NativeOwnershipConformance.OpenAsync(root, token);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        await using var reader = manager.FindBySubjectAsync(NativeOwnershipConformance.Subject, token).GetAsyncEnumerator(token);
        Assert.True(await reader.MoveNextAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync(token));
    }

    /// <summary>An external transaction cannot replace the exact owner transaction merely by remaining non-null.</summary>
    private static async Task RejectReplacedTransactionAsync(IServiceProvider root, CancellationToken token)
    {
        await using var scope = await NativeOwnershipConformance.OpenAsync(root, token);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        var original = scope.Owner.Database.Database.CurrentTransaction!;
        await original.DisposeAsync();
        await using var replacement = await scope.Owner.Database.Database.BeginTransactionAsync(token);
        Assert.NotSame(original, replacement);
        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync(token));
    }
}

/// <summary>Injects cancellation at actual EF update execution; it never substitutes persistence or manager behavior.</summary>
internal sealed class CancelNativeUpdate : SaveChangesInterceptor
{
    private const string LostCancellation = "Native update lost its owner cancellation.";
    internal int Calls
    {
        get; private set;
    }
    internal CancellationToken ReceivedToken
    {
        get; private set;
    }
    internal OperationCanceledException? Cancellation
    {
        get; private set;
    }

    /// <summary>Resets every observation before an independent native manager attempt.</summary>
    internal void Arm(CancellationTokenSource request)
    {
        Request = request;
        Calls = 0;
        Cancellation = null;
        ReceivedToken = default;
        ObservedLinkedCancellation = false;
    }

    internal CancellationTokenSource? Request
    {
        get; set;
    }
    internal bool ObservedLinkedCancellation
    {
        get; private set;
    }

    /// <summary>Cancels the original request and proves the native update received its linked owner token.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Request is not null)
        {
            Calls++;
            ReceivedToken = cancellationToken;
            Request.Cancel();
            ObservedLinkedCancellation = cancellationToken.IsCancellationRequested;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException error) { Cancellation = error; throw; }
            throw new InvalidOperationException(LostCancellation);
        }
        return new(result);
    }
}
