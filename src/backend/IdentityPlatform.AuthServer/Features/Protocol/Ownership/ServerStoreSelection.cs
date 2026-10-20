using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Ownership;

/// <summary>Chooses immutable scope storage after native extraction and before validation can resolve its first manager.</summary>
internal sealed class ServerStoreSelection(ProtocolStorageSelection selection, ProtocolProfiles? profiles = null, ProtocolHostRequest? host = null)
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
        if (profiles is not null)
        {
            if (profiles.Pilot && pilot && host?.IsPilot == true)
            {
                host.RequirePilotExtraction(context);
                selection.Require(ProtocolStorageMode.LegacyPilot);
                return ValueTask.CompletedTask;
            }
            selection.Select(ProtocolStorageMode.Owned);
            if (context.EndpointType is not (OpenIddictServerEndpointType.Configuration or OpenIddictServerEndpointType.JsonWebKeySet) &&
                (profiles.Administrator is null || client.ValueKind != JsonValueKind.String || client.GetString() != AdministratorProtocolIdentity.ClientId))
            {
                context.Reject(OpenIddictConstants.Errors.InvalidClient);
            }
        }
        else
        {
            selection.Select(pilot ? ProtocolStorageMode.LegacyPilot : ProtocolStorageMode.Owned);
        }
        return ValueTask.CompletedTask;
    }
}
