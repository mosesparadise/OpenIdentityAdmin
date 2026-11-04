using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Commits native managed validation/replay revocation and optional redemption in a fresh child before any issuance epoch.</summary>
internal static class ManagedDurableToken
{
    /// <summary>Runs actual native handlers, preserving rejection-side writes and withholding success until actual transaction/context cleanup.</summary>
    internal static async ValueTask<ManagedTokenBinding?> RunAsync(IServiceScopeFactory factory,
        OpenIddictServerEvents.ValidateTokenContext validation, OpenIddictServerEvents.ProcessSignInContext? signIn = null)
    {
        var result = await ProtocolStorageChild.RunAsync(factory, async (scope, token) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            NativeErrorOrigin.RecordPhase(validation.Transaction, scope.Owner);
            await ManagedRegistration.LockAsync(scope, token);
            var binding = validation.Principal is null || validation.TokenId != validation.Principal.GetTokenId() ||
                validation.AuthorizationId != validation.Principal.GetAuthorizationId() || validation.Transaction.Request?.ClientId is not { } client
                ? null : await ManagedTokenBinding.ReadAsync(scope, validation.Principal, client, token);
            if (binding is null)
            {
                validation.Reject(OpenIddictConstants.Errors.InvalidToken);
            }
            else
            {
                await scope.Services.GetRequiredService<OpenIddictServerHandlers.Protection.ValidateTokenEntry>().HandleAsync(validation);
                if (!validation.IsRejected && validation.AuthorizationId is not null)
                {
                    await scope.Services.GetRequiredService<OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry>().HandleAsync(validation);
                }
                if (!validation.IsRejected && signIn is not null)
                {
                    await scope.Services.GetRequiredService<OpenIddictServerHandlers.RedeemTokenEntry>().HandleAsync(signIn);
                }
                if (binding.Expiration <= scope.Owner.Clock.GetUtcNow())
                {
                    validation.Reject(OpenIddictConstants.Errors.InvalidToken);
                }
            }
            await transaction.CommitAsync(token);
            return binding;
        });
        if (result is null || result.Expiration <= ProtocolStorageAmbient.Current!.Request.Clock.GetUtcNow())
        {
            validation.Reject(OpenIddictConstants.Errors.InvalidToken);
        }
        return validation.IsRejected ? null : result;
    }
}
