using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Extends only explicitly enabled pilot replay cleanup while retaining native validation filters and rejection behavior.</summary>
internal static class Setup
{
    /// <summary>Runs after native cryptographic/proof validation and token-ID resolution, immediately before native lifecycle validation.</summary>
    internal static OpenIddictServerBuilder AddPilotReplayCleanup(this OpenIddictServerBuilder builder, bool standalone)
    {
        var native = OpenIddictServerHandlers.Protection.ValidateTokenEntry.Descriptor;
        builder.Services.TryAddScoped<PilotReplayCleanup>();
        builder.Services.TryAddScoped<OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry>();
        if (!standalone)
        {
            builder.Services.TryAddScoped<PilotTokenValidation>();
            return builder;
        }
        builder.RemoveEventHandler(native);
        builder.Services.TryAddScoped<OpenIddictServerHandlers.Protection.ValidateTokenEntry>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenContext>()
            .Import(native).UseScopedHandler<PilotTokenValidation>().SetType(OpenIddictServerHandlerType.Custom).Build());
        return builder;
    }
}
