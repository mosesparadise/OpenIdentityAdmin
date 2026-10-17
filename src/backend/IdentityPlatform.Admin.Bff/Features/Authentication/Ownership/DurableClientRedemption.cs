using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Consumes native callback state durably before correlation/error processing or any backchannel request.</summary>
internal sealed class DurableClientRedemption(IServiceScopeFactory factory, OpenIddictClientHandlers.RedeemStateTokenEntry native)
    : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessAuthenticationContext>
{
    /// <summary>Uses the validated state's registration binding because native registration resolution has not run at this slot.</summary>
    public async ValueTask HandleAsync(OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        var principal = context.StateTokenPrincipal;
        if (principal is not null && !string.Equals(principal.GetClaim(OpenIddictConstants.Claims.Private.RegistrationId),
            AdministratorProtocolIdentity.ClientId, StringComparison.Ordinal))
        {
            await native.HandleAsync(context);
            return;
        }
        if (principal is null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
            return;
        }
        var clock = ProtocolStorageAmbient.Current?.Request.Clock ?? throw ProtocolStorageOwner.Unavailable();
        var expiration = await ProtocolStorageChild.RunAsync(factory, async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireClientStateAsync(scope.Owner, cancellation);
            var bound = await AdministratorNativeToken.ExpirationAsync(scope, principal,
                OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken, applicationId: null, cancellation);
            if (bound is not null)
            {
                await ConsumeAsync(scope.Services, context);
            }
            else
            {
                context.Reject(OpenIddictConstants.Errors.InvalidToken);
            }
            if (!AdministratorNativeToken.IsCurrent(bound, scope.Owner.Clock))
            {
                context.Reject(OpenIddictConstants.Errors.InvalidToken);
            }
            await transaction.CommitAsync(cancellation);
            return bound;
        });
        if (!AdministratorNativeToken.IsCurrent(expiration, clock))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
        }
    }

    /// <summary>Calls only the actual native database-status check before actual native redemption in the same fresh tracked context.</summary>
    private static async ValueTask ConsumeAsync(IServiceProvider services, OpenIddictClientEvents.ProcessAuthenticationContext context)
    {
        var validation = new OpenIddictClientEvents.ValidateTokenContext(context.Transaction)
        {
            Principal = context.StateTokenPrincipal,
            TokenId = context.StateTokenPrincipal!.GetTokenId()
        };
        await services.GetRequiredService<OpenIddictClientHandlers.Protection.ValidateTokenEntry>().HandleAsync(validation);
        if (validation.IsRejected)
        {
            context.Reject(validation.Error, validation.ErrorDescription, validation.ErrorUri);
            return;
        }
        await services.GetRequiredService<OpenIddictClientHandlers.RedeemStateTokenEntry>().HandleAsync(context);
    }
}
