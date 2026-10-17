using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Preserves the native validation slot while isolating its replay-revocation writes from later issuance rollback.</summary>
internal sealed class DurableServerValidation(IServiceScopeFactory factory, OpenIddictServerHandlers.Protection.ValidateTokenEntry native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    /// <summary>Delegates other profiles unchanged and commits fixed-profile native validation in its own bounded child.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context) =>
        DurableServerToken.IsAdministrator(context.Principal) ? DurableServerToken.RunAsync(factory, context) : native.HandleAsync(context);
}
