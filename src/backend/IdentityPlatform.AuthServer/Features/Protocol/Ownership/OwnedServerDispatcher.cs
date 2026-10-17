using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Forwards the installed native server dispatcher only within explicitly owned protocol storage.</summary>
internal sealed class OwnedServerDispatcher(OpenIddictServerDispatcher native, ProtocolStorageOwner owner) : IOpenIddictServerDispatcher
{
    /// <summary>Preserves native event types/outcomes and nested cancellation; the enclosing request owns final cleanup.</summary>
    public ValueTask DispatchAsync<TContext>(TContext context) where TContext : OpenIddictServerEvents.BaseContext
    {
        ArgumentNullException.ThrowIfNull(context);
        return ProtocolStorageDispatch.RunAsync(owner, () => context.CancellationToken,
            token => context.CancellationToken = token, () => native.DispatchAsync(context));
    }
}
