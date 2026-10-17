using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Rejects profile switching before any sign-in persistence can use a store selected for a different extracted request.</summary>
internal sealed class ServerStoreFence(ProtocolStorageSelection selection)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Requires the resolved native profile to agree with immutable scope selection, including cached request restoration.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var pilotEndpoint = context.EndpointType is OpenIddictServerEndpointType.Authorization or OpenIddictServerEndpointType.Token;
        selection.Require(pilotEndpoint && string.Equals(context.ClientId, PilotProfile.ClientId, StringComparison.Ordinal)
            ? ProtocolStorageMode.LegacyPilot : ProtocolStorageMode.Owned);
        return ValueTask.CompletedTask;
    }
}
