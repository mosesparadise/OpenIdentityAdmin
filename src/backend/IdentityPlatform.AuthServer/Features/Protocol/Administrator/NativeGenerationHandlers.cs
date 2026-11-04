using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Observes the installed native row creator without replacing its managers, filters or order.</summary>
internal sealed class NativeTokenCreation(OpenIddictServerHandlers.Protection.CreateTokenEntry native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
{
    /// <summary>Delegates nonadministrator generation unchanged and retains exact creation evidence for active owned issuance.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context) =>
        NativeTokenObservations.ObserveAsync(context, entry => entry.CreateAsync(() => native.HandleAsync(context)), () => native.HandleAsync(context));
}

/// <summary>Observes actual native cryptographic generation, rejecting its preset-output skip during owned issuance.</summary>
internal sealed class NativeTokenSigning(OpenIddictServerHandlers.Protection.GenerateIdentityModelToken native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
{
    /// <summary>Preserves the native signer and token handler while recording only its completed output.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context) =>
        NativeTokenObservations.ObserveAsync(context, entry => entry.SignAsync(() => native.HandleAsync(context)), () => native.HandleAsync(context));
}

/// <summary>Observes native protected-payload persistence and reference substitution in the actual owning transaction.</summary>
internal sealed class NativeTokenPayload(OpenIddictServerHandlers.Protection.AttachTokenPayload native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.GenerateTokenContext>
{
    /// <summary>Leaves native reference hashing and payload update intact rather than reimplementing either store behavior.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.GenerateTokenContext context) =>
        NativeTokenObservations.ObserveAsync(context, entry => entry.PersistAsync(() => native.HandleAsync(context)), () => native.HandleAsync(context));
}
