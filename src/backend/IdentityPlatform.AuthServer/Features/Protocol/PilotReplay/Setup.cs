using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Extends only explicitly enabled pilot replay cleanup while retaining native validation filters and rejection behavior.</summary>
internal static class Setup
{
    /// <summary>Runs after native cryptographic/proof validation and token-ID resolution, immediately before native lifecycle validation.</summary>
    internal static OpenIddictServerBuilder AddPilotReplayCleanup(this OpenIddictServerBuilder builder)
    {
        var native = OpenIddictServerHandlers.Protection.ValidateTokenEntry.Descriptor;
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenContext>()
            .Import(native).UseScopedHandler<PilotReplayCleanup>().SetOrder(native.Order - 1).SetType(OpenIddictServerHandlerType.Custom).Build());
        return builder;
    }
}
