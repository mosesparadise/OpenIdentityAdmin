using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Defers expiration denial only for cryptographically validated fixed-client state until its native row has been durably consumed.</summary>
internal sealed class NativeClientStateExpiration(OpenIddictClientHandlers.Protection.ValidateExpirationDate native)
    : IOpenIddictClientHandler<OpenIddictClientEvents.ValidateTokenContext>
{
    /// <summary>Preserves signature/issuer/type validation and delegates every other token or registration to the original native lifetime handler.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ValidateTokenContext context)
    {
        if (context.ValidTokenTypes.Count == 1 && context.ValidTokenTypes.Contains(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken) &&
            context.Principal?.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken) == true &&
            context.Principal.GetClaim(OpenIddictConstants.Claims.Private.RegistrationId) == AdministratorProtocolIdentity.ClientId)
        {
            return ValueTask.CompletedTask;
        }
        return native.HandleAsync(context);
    }
}
