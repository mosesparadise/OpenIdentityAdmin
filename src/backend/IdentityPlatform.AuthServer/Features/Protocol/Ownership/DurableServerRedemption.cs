using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Forwards actual native early redemption after fresh serialized validation of the same tracked token row.</summary>
internal sealed class DurableServerRedemption(IServiceScopeFactory factory, OpenIddictServerHandlers.RedeemTokenEntry native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Retains the original native transaction and principal while moving only durable consumption into a fresh child.</summary>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (!string.Equals(context.ClientId, AdministratorProtocolIdentity.ClientId, StringComparison.Ordinal) ||
            (context.EndpointType != OpenIddictServerEndpointType.Authorization &&
                !(context.EndpointType == OpenIddictServerEndpointType.Token && context.Request.IsAuthorizationCodeGrantType())))
        {
            await native.HandleAsync(context);
            return;
        }
        var authentication = context.Transaction.GetProperty<OpenIddictServerEvents.ProcessAuthenticationContext>(
            typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!);
        var principal = context.EndpointType switch
        {
            OpenIddictServerEndpointType.Authorization => authentication?.RequestTokenPrincipal,
            OpenIddictServerEndpointType.Token when context.Request.IsAuthorizationCodeGrantType() => authentication?.AuthorizationCodePrincipal,
            _ => null
        };
        if (principal is null || !DurableServerToken.IsAdministrator(principal))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
            return;
        }
        var validation = new OpenIddictServerEvents.ValidateTokenContext(context.Transaction)
        {
            Principal = principal,
            TokenId = principal!.GetTokenId(),
            AuthorizationId = principal.GetAuthorizationId()
        };
        await DurableServerToken.RunAsync(factory, validation,
            services => services.GetRequiredService<OpenIddictServerHandlers.RedeemTokenEntry>().HandleAsync(context));
        if (validation.IsRejected)
        {
            context.Reject(validation.Error, validation.ErrorDescription, validation.ErrorUri);
        }
    }
}
