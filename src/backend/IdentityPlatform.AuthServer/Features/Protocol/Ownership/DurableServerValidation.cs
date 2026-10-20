using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Preserves the native validation slot while isolating its replay-revocation writes from later issuance rollback.</summary>
internal sealed class DurableServerValidation(IServiceScopeFactory factory, OpenIddictServerHandlers.Protection.ValidateTokenEntry native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    /// <summary>Delegates other profiles unchanged and commits fixed-profile native validation in its own bounded child.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        if (context.Principal is { } principal && principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.IdentityToken) &&
            principal.GetAudiences().Contains(AdministratorProtocolIdentity.ClientId, StringComparer.Ordinal))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
            return ValueTask.CompletedTask;
        }
        if (!DurableServerToken.IsAdministrator(context.Principal))
        {
            return native.HandleAsync(context);
        }
        if (!string.Equals(context.Transaction.Request?.ClientId, AdministratorProtocolIdentity.ClientId, StringComparison.Ordinal))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
            return ValueTask.CompletedTask;
        }
        return DurableServerToken.RunAsync(factory, context);
    }
}
