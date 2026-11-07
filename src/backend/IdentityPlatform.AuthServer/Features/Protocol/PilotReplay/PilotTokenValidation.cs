using IdentityPlatform.AuthServer.Features.Pilot;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Runs initial pilot lifecycle validation and replay cleanup in one fresh gate-owning native scope.</summary>
internal sealed class PilotTokenValidation(IServiceScopeFactory factory, TimeProvider clock,
    OpenIddictServerHandlers.Protection.ValidateTokenEntry native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    /// <summary>Routes only pilot code exchanges; every other token preserves its existing native validation path.</summary>
    internal static bool Candidate(OpenIddictServerEvents.ValidateTokenContext context) =>
        context.EndpointType == OpenIddictServerEndpointType.Token && context.Transaction.Request is { } request &&
        request.IsAuthorizationCodeGrantType() && request.ClientId == PilotProfile.ClientId &&
        context.Principal?.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode) == true;

    /// <summary>Uses the existing native slot after cryptographic validation and waits for transaction and scope cleanup before returning.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context) => Candidate(context)
        ? PilotProtocolTransaction.RunAsync(factory, clock, (services, token) => ValidateAsync(services, context, token), context.CancellationToken)
        : native.HandleAsync(context);

    /// <summary>Freshly binds all native identifiers to the persisted pilot before native validation can revoke a chain.</summary>
    internal static async ValueTask ValidateAsync(IServiceProvider services, OpenIddictServerEvents.ValidateTokenContext context, CancellationToken token)
    {
        var original = context.CancellationToken;
        context.CancellationToken = token;
        try
        {
            var cleanup = services.GetRequiredService<PilotReplayCleanup>();
            if (!PilotReplayCleanup.Matches(context) || !await cleanup.IsBoundAsync(context, requireRevoked: false) ||
                context.Principal!.GetExpirationDate() is not { } expiration || expiration <= services.GetRequiredService<TimeProvider>().GetUtcNow())
            {
                context.Reject(OpenIddictConstants.Errors.InvalidToken);
                return;
            }
            await cleanup.HandleAsync(context);
            await services.GetRequiredService<OpenIddictServerHandlers.Protection.ValidateTokenEntry>().HandleAsync(context);
            if (!context.IsRejected)
            {
                await services.GetRequiredService<OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry>().HandleAsync(context);
            }
        }
        finally { context.CancellationToken = original; }
    }
}
