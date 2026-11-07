using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Preserves the native validation slot while isolating its replay-revocation writes from later issuance rollback.</summary>
internal sealed class DurableServerValidation(IServiceScopeFactory factory, OpenIddictServerHandlers.Protection.ValidateTokenEntry native,
    PilotTokenValidation? pilot = null)
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
        if (pilot is not null && PilotTokenValidation.Candidate(context))
        {
            return pilot.HandleAsync(context);
        }
        if (ManagedTokenBinding.Candidate(context.Principal))
        {
            return ValidateManagedAsync(context);
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
    /// <summary>Isolates managed replay validation without redeeming a valid artifact before client authentication and PKCE finish.</summary>
    private async ValueTask ValidateManagedAsync(OpenIddictServerEvents.ValidateTokenContext context) => await ManagedDurableToken.RunAsync(factory, context);

}
