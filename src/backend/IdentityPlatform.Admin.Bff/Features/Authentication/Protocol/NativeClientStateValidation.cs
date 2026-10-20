using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Preserves native state validation while avoiding the pinned library's premature registration dereference on a missing signing key.</summary>
internal sealed class NativeClientStateValidation(OpenIddictClientHandlers.Protection.ValidateIdentityModelToken native)
    : IOpenIddictClientHandler<OpenIddictClientEvents.ValidateTokenContext>
{
    /// <summary>Supplies no trust or refresh source; native global state parameters remain authoritative and registration is restored before later handlers.</summary>
    public async ValueTask HandleAsync(OpenIddictClientEvents.ValidateTokenContext context)
    {
        var original = context.Registration;
        if (original is not null || !context.ValidTokenTypes.SetEquals([OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken]))
        {
            await native.HandleAsync(context);
            return;
        }
        // OpenIddict 7.7.1 dereferences Registration when deciding whether an unknown signing key should refresh remote metadata.
        // State validation precedes registration resolution and uses only already-resolved local state keys. Reassess on upgrade.
        context.Transaction.Registration = new OpenIddictClientRegistration();
        try
        {
            await native.HandleAsync(context);
        }
        finally { context.Transaction.Registration = original!; }
    }
}
