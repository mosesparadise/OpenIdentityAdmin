using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Observes actual native credentials preparation and preserves its installed pipeline slot.</summary>
internal sealed class NativeTokenCredentials(OpenIddictServerHandlers.Protection.AttachSecurityCredentials native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
{
    /// <summary>Freezes the actual descriptor between native stages only for privately owned administrator generation.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context) =>
        NativeTokenObservations.ObserveAsync(context, entry => entry.PrepareAsync(ProtocolTokenPreparation.Stage.Credentials, () => native.HandleAsync(context)), () => native.HandleAsync(context));
}

/// <summary>Observes actual native subject preparation and preserves its installed pipeline slot.</summary>
internal sealed class NativeTokenSubject(OpenIddictServerHandlers.Protection.AttachTokenSubject native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
{
    /// <summary>Freezes the actual descriptor between native stages only for privately owned administrator generation.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context) =>
        NativeTokenObservations.ObserveAsync(context, entry => entry.PrepareAsync(ProtocolTokenPreparation.Stage.Subject, () => native.HandleAsync(context)), () => native.HandleAsync(context));
}

/// <summary>Observes actual native metadata preparation and preserves its installed pipeline slot.</summary>
internal sealed class NativeTokenMetadata(OpenIddictServerHandlers.Protection.AttachTokenMetadata native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
{
    /// <summary>Freezes the actual descriptor between native stages only for privately owned administrator generation.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context) =>
        NativeTokenObservations.ObserveAsync(context, entry => entry.PrepareAsync(ProtocolTokenPreparation.Stage.Metadata, () => native.HandleAsync(context)), () => native.HandleAsync(context));
}
