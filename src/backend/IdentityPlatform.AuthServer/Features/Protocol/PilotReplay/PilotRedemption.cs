using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Preserves the native redemption descriptor while coordinating only pilot code exchange.</summary>
internal sealed class PilotRedemption(PilotGeneration generation, OpenIddictServerHandlers.RedeemTokenEntry native)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Uses the private generation continuation for code exchange and leaves other native ceremonies unchanged.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context) => PilotExchange.Candidate(context)
        ? generation.RedeemAsync(context) : native.HandleAsync(context);
}
