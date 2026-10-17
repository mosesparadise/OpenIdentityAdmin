using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Chooses immutable scope storage after native extraction and before validation can resolve its first manager.</summary>
internal sealed class ServerStoreSelection(ProtocolStorageSelection selection)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessRequestContext>
{
    internal const int AfterExtraction = 500;

    /// <summary>Only an exact extracted pilot client on its approved protocol endpoints may select the explicit legacy mode.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessRequestContext context)
    {
        if (context.EndpointType == OpenIddictServerEndpointType.Unknown)
        {
            return ValueTask.CompletedTask;
        }
        var pilotEndpoint = context.EndpointType is OpenIddictServerEndpointType.Authorization or OpenIddictServerEndpointType.Token;
        var client = (JsonElement)context.Transaction.Request?.GetParameter(OpenIddictConstants.Parameters.ClientId);
        var pilot = pilotEndpoint && client.ValueKind == JsonValueKind.String &&
            string.Equals(client.GetString(), PilotProfile.ClientId, StringComparison.Ordinal);
        selection.Select(pilot ? ProtocolStorageMode.LegacyPilot : ProtocolStorageMode.Owned);
        return ValueTask.CompletedTask;
    }
}
