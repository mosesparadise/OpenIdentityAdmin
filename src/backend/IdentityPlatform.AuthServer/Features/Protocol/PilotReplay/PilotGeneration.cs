using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Provides only the generation scope's one-use native redemption continuation.</summary>
internal sealed class PilotGeneration(IServiceProvider services, IServiceScopeFactory factory)
{
    private PilotContinuation? continuation;
    private int redeemed;

    /// <summary>Binds exactly one protected native sign-in after gate acquisition and fresh validation.</summary>
    internal void Bind(PilotContinuation value)
    {
        if (continuation is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        continuation = value;
    }

    /// <summary>Moves native consumption into its durable child, then reloads all generation authority under the still-held gate.</summary>
    internal async ValueTask RedeemAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var current = continuation ?? throw ProtocolStorageOwner.Unavailable();
        if (!ReferenceEquals(current.Context, context) || Interlocked.Exchange(ref redeemed, 1) != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        current.RequireCurrent();
        PilotStorageFence.Current!.RequireCurrent();
        await PilotConsumption.RunAsync(factory, current, context.CancellationToken);
        PilotStorageFence.Current!.RequireCurrent();
        if (context.IsRejected)
        {
            return;
        }
        PilotAuthority.Refresh(services);
        if (!current.Consumed || !await PilotAuthority.IsCurrentAsync(services, current, OpenIddictConstants.Statuses.Redeemed, context.CancellationToken))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant);
        }
    }
}
