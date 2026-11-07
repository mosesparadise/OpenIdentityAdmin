using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Coordinates standalone pilot exchanges using actual native dispatch and preserves other native endpoints.</summary>
internal sealed class PilotServerDispatcher(OpenIddictServerDispatcher native, PilotIssuanceCoordinator coordinator) : IOpenIddictServerDispatcher
{
    /// <summary>Tracks real native completion and intercepts only the original pilot code sign-in.</summary>
    public async ValueTask DispatchAsync<TContext>(TContext context) where TContext : OpenIddictServerEvents.BaseContext
    {
        var exchange = PilotExchange.Current;
        exchange?.Before(context);
        if (context is OpenIddictServerEvents.ProcessSignInContext signIn && PilotExchange.Candidate(signIn))
        {
            await coordinator.RunAsync(signIn);
        }
        else
        {
            await native.DispatchAsync(context);
        }
        exchange?.After(context);
    }
}
