using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Forwards the installed native server dispatcher only within explicitly owned protocol storage.</summary>
internal sealed class OwnedServerDispatcher(OpenIddictServerDispatcher native, ProtocolStorageOwner owner, IServiceProvider services) : IOpenIddictServerDispatcher
{
    /// <summary>Preserves native event types/outcomes and nested cancellation; the enclosing request owns final cleanup.</summary>
    public ValueTask DispatchAsync<TContext>(TContext context) where TContext : OpenIddictServerEvents.BaseContext
    {
        ArgumentNullException.ThrowIfNull(context);
        return ProtocolStorageDispatch.RunAsync(owner, () => context.CancellationToken,
            token => context.CancellationToken = token, () => DispatchNativeAsync(context));
    }

    /// <summary>Routes only explicitly enabled fixed-client PAR issuance into a fresh child; all other native paths preserve their dispatcher.</summary>
    private ValueTask DispatchNativeAsync<TContext>(TContext context) where TContext : OpenIddictServerEvents.BaseContext
    {
        if (context is OpenIddictServerEvents.ProcessSignInContext signIn && signIn.EndpointType == OpenIddictServerEndpointType.PushedAuthorization &&
            signIn.ClientId == AdministratorProtocolIdentity.ClientId && services.GetService<AdministratorIssuanceCoordinator>() is { } coordinator)
        {
            return coordinator.RunAsync(signIn);
        }
        return context is OpenIddictServerEvents.GenerateTokenContext generation && NativeIssuanceProof.Find(context.Transaction) is { } proof
            ? proof.GenerateAsync(generation, () => native.DispatchAsync(context)) : native.DispatchAsync(context);
    }
}
