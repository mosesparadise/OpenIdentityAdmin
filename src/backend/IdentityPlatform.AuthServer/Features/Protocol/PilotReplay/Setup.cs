using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        builder.Services.TryAddScoped<PilotGeneration>();
        builder.Services.TryAddScoped<PilotIssuanceCoordinator>();
        builder.Services.TryAddScoped<OpenIddictServerDispatcher>();
        builder.Services.AddDbContext<PlatformDbContext>(options => options.AddInterceptors(new PilotCommandFence(), new PilotConnectionFence(), new PilotCommitFence()));
        builder.Services.TryAddScoped<PilotReplayCleanup>();
        builder.Services.TryAddScoped<OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry>();
        if (!standalone)
        {
            builder.Services.TryAddScoped<PilotTokenValidation>();
            return builder;
        }
        builder.RemoveEventHandler(native);
        var redemption = OpenIddictServerHandlers.RedeemTokenEntry.Descriptor;
        builder.RemoveEventHandler(redemption);
        builder.Services.TryAddScoped<OpenIddictServerHandlers.RedeemTokenEntry>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessSignInContext>()
            .Import(redemption).UseScopedHandler<PilotRedemption>().SetType(OpenIddictServerHandlerType.Custom).Build());
        builder.Services.Replace(ServiceDescriptor.Scoped<IOpenIddictServerDispatcher, PilotServerDispatcher>());
        builder.Services.TryAddScoped<OpenIddictServerHandlers.Protection.ValidateTokenEntry>();
        builder.AddEventHandler(OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenContext>()
            .Import(native).UseScopedHandler<PilotTokenValidation>().SetType(OpenIddictServerHandlerType.Custom).Build());
        return builder;
    }
}
