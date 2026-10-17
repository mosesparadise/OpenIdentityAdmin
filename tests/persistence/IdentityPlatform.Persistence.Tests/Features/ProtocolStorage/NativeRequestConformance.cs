using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Proves fresh owned scopes retain earlier durable native state while rolling back a later failed phase.</summary>
internal static class NativeRequestConformance
{
    private const string PendingScope = "pending-dispatch-rollback";
    private const string Client = "shared-request-client";
    private const string RolledBackScope = "shared-request-rollback";

    /// <summary>Uses one original request for commit and later failure, then independently observes both durable outcomes.</summary>
    internal static async Task VerifyAsync(ServiceProvider root, CancellationToken token)
    {
        var factory = root.GetRequiredService<IServiceScopeFactory>();
        var request = new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token);
        var id = await request.RunAsync(factory, CreateApplicationAsync);
        var expected = new ArithmeticException();
        Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => request.RunAsync<int>(factory, async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await scope.Services.GetRequiredService<IOpenIddictScopeManager>().CreateAsync(new OpenIddictScopeDescriptor { Name = RolledBackScope }, cancellation);
            throw expected;
        })));
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(factory, (_, _) => new ValueTask<int>(1)));
        await VerifyPendingCommitAsync(factory, root.GetRequiredService<TimeProvider>(), token);
        var observation = new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token);
        await observation.RunAsync(factory, async (scope, cancellation) =>
        {
            var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await applications.FindByClientIdAsync(Client, cancellation);
            Assert.NotNull(application);
            Assert.Equal(id, await applications.GetIdAsync(application, cancellation));
            Assert.Null(await scope.Services.GetRequiredService<IOpenIddictScopeManager>().FindByNameAsync(RolledBackScope, cancellation));
            Assert.Null(await scope.Services.GetRequiredService<IOpenIddictScopeManager>().FindByNameAsync(PendingScope, cancellation));
            return 1;
        });
    }

    /// <summary>Rejects a real commit outside the pending dispatcher and proves its native write rolls back.</summary>
    private static async Task VerifyPendingCommitAsync(IServiceScopeFactory factory, TimeProvider clock, CancellationToken token)
    {
        var request = new ProtocolStorageRequest(clock, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(factory, async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await scope.Services.GetRequiredService<IOpenIddictScopeManager>().CreateAsync(new OpenIddictScopeDescriptor { Name = PendingScope }, cancellation);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var binding = cancellation;
            var pending = ProtocolStorageDispatch.RunAsync(scope.Owner, () => binding, value => binding = value, () => new(release.Task)).AsTask();
            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync(cancellation));
            }
            finally { release.TrySetResult(); }
            await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
            return 1;
        }));
    }

    /// <summary>Completes a real transaction inside the active dispatch leaf, including phase cleanup before dispatch returns.</summary>
    private static async ValueTask<string?> CreateApplicationAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        var binding = token;
        string? id = null;
        await ProtocolStorageDispatch.RunAsync(scope.Owner, () => binding, value => binding = value, async () =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = Client,
                ClientType = OpenIddictConstants.ClientTypes.Public
            }, binding);
            id = await manager.GetIdAsync(application, binding);
            await transaction.CommitAsync(binding);
        });
        return id;
    }
}
