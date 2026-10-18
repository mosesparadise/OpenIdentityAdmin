using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Observes actual native credentials preparation and preserves its installed pipeline slot.</summary>
internal sealed class NativeStateCredentials(OpenIddictClientHandlers.Protection.AttachSecurityCredentials native)
    : IOpenIddictClientHandler<OpenIddictClientEvents.GenerateTokenContext>
{
    /// <summary>Freezes the actual descriptor between native stages only for privately owned administrator generation.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.GenerateTokenContext context) =>
        NativeStateGeneration.Find(context.Transaction) is { } proof
            ? proof.PrepareAsync(context, ProtocolTokenPreparation.Stage.Credentials, () => native.HandleAsync(context)) : native.HandleAsync(context);
}

/// <summary>Observes actual native subject preparation and preserves its installed pipeline slot.</summary>
internal sealed class NativeStateSubject(OpenIddictClientHandlers.Protection.AttachTokenSubject native)
    : IOpenIddictClientHandler<OpenIddictClientEvents.GenerateTokenContext>
{
    /// <summary>Freezes the actual descriptor between native stages only for privately owned administrator generation.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.GenerateTokenContext context) =>
        NativeStateGeneration.Find(context.Transaction) is { } proof
            ? proof.PrepareAsync(context, ProtocolTokenPreparation.Stage.Subject, () => native.HandleAsync(context)) : native.HandleAsync(context);
}

/// <summary>Observes actual native metadata preparation and preserves its installed pipeline slot.</summary>
internal sealed class NativeStateMetadata(OpenIddictClientHandlers.Protection.AttachTokenMetadata native)
    : IOpenIddictClientHandler<OpenIddictClientEvents.GenerateTokenContext>
{
    /// <summary>Freezes the actual descriptor between native stages only for privately owned administrator generation.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.GenerateTokenContext context) =>
        NativeStateGeneration.Find(context.Transaction) is { } proof
            ? proof.PrepareAsync(context, ProtocolTokenPreparation.Stage.Metadata, () => native.HandleAsync(context)) : native.HandleAsync(context);
}
