using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Forwards the installed native client dispatcher only within explicitly owned protocol storage.</summary>
internal sealed class OwnedClientDispatcher(OpenIddictClientDispatcher native, ProtocolStorageOwner owner) : IOpenIddictClientDispatcher
{
    /// <summary>Preserves native event types/outcomes and nested cancellation; the enclosing request owns final cleanup.</summary>
    public ValueTask DispatchAsync<TContext>(TContext context) where TContext : OpenIddictClientEvents.BaseContext
    {
        ArgumentNullException.ThrowIfNull(context);
        return ProtocolStorageDispatch.RunAsync(owner, () => context.CancellationToken,
            token => context.CancellationToken = token, () =>
                context is OpenIddictClientEvents.GenerateTokenContext generation && NativeStateGeneration.Find(context.Transaction) is { } proof
                    ? proof.GenerateAsync(generation, () => native.DispatchAsync(context)) : native.DispatchAsync(context));
    }
}
