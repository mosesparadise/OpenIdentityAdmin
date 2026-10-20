using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Hosting;

/// <summary>Installs shared native hosting only when the independent administrator protocol switch is enabled.</summary>
internal static class Setup
{
    /// <summary>Registers per-request transport identity and a post-inference fence ahead of every native extractor.</summary>
    internal static OpenIddictServerBuilder AddProtocolHosting(this OpenIddictServerBuilder builder)
    {
        builder.Services.AddScoped<ProtocolHostRequest>();
        builder.AddEventHandler<OpenIddictServerEvents.ProcessRequestContext>(handler => handler.UseScopedHandler<ProtocolInferenceGuard>()
            .SetOrder(OpenIddictServerHandlers.InferEndpointType.Descriptor.Order + 1));
        return builder;
    }

    /// <summary>Installs ownership before authentication while preserving the shared account request guards that precede it.</summary>
    internal static WebApplication UseProtocolHosting(this WebApplication app)
    {
        app.Use(ProtocolHosting.InvokeAsync);
        return app;
    }

    /// <summary>Maps profile-dispatching decisions without binding pilot-only services in administrator-only hosts.</summary>
    internal static WebApplication MapSharedProtocol(this WebApplication app)
    {
        app.MapMethods(ProtocolOptions.AuthorizePath, [HttpMethods.Get, HttpMethods.Post], (Delegate)ProtocolDecisions.AuthorizeAsync);
        app.MapPost(ProtocolOptions.TokenPath, (Delegate)ProtocolDecisions.ExchangeAsync);
        return app;
    }
}
