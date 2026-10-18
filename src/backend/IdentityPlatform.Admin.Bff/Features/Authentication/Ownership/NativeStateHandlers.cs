using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Retains installed native state-row creation and its exact descriptor filters/order.</summary>
internal sealed class NativeStateCreation(OpenIddictClientHandlers.Protection.CreateTokenEntry native)
    : IOpenIddictClientHandler<OpenIddictClientEvents.GenerateTokenContext>
{
    /// <summary>Observes only the current privately bound generation; other native generation retains its installed behavior.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.GenerateTokenContext context) =>
        NativeStateGeneration.Find(context.Transaction) is { } proof ? proof.CreateAsync(context, () => native.HandleAsync(context)) : native.HandleAsync(context);
}

/// <summary>Retains native signing/encryption while rejecting its preset-output shortcut for bounded state creation.</summary>
internal sealed class NativeStateSigning(OpenIddictClientHandlers.Protection.GenerateIdentityModelToken native)
    : IOpenIddictClientHandler<OpenIddictClientEvents.GenerateTokenContext>
{
    /// <summary>Records the actual native cryptographic output before reference substitution.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.GenerateTokenContext context) =>
        NativeStateGeneration.Find(context.Transaction) is { } proof ? proof.SignAsync(context, () => native.HandleAsync(context))
            : NativeClientAssertion.Find(context.Transaction) is { } assertion
                ? assertion.SignAsync(context, () => native.HandleAsync(context)) : native.HandleAsync(context);
}

/// <summary>Retains native payload persistence and opaque-reference generation.</summary>
internal sealed class NativeStatePayload(OpenIddictClientHandlers.Protection.AttachTokenPayload native)
    : IOpenIddictClientHandler<OpenIddictClientEvents.GenerateTokenContext>
{
    /// <summary>Requires the exact observed cryptographic output when updating the manager-owned row.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.GenerateTokenContext context) =>
        NativeStateGeneration.Find(context.Transaction) is { } proof ? proof.PersistAsync(context, () => native.HandleAsync(context)) : native.HandleAsync(context);
}
