using OpenIddict.Server.AspNetCore;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityPlatform.AuthServer.Features.Protocol.Token;

internal static class Setup
{
    internal static OpenIddictServerBuilder AddTokenExtraction(this OpenIddictServerBuilder options)
    {
        var order = OpenIddictServerAspNetCoreHandlers.ExtractBasicAuthenticationCredentials<ExtractTokenRequestContext>.Descriptor.Order;
        options.AddEventHandler<ExtractTokenRequestContext>(handler => handler
            .AddFilter<OpenIddictServerAspNetCoreHandlerFilters.RequireHttpRequest>()
            .SetOrder(order - 1).UseInlineHandler(ClientIdentityConsistency.CaptureAsync));
        options.AddEventHandler<ExtractTokenRequestContext>(handler => handler
            .AddFilter<OpenIddictServerAspNetCoreHandlerFilters.RequireHttpRequest>()
            .SetOrder(order + 1).UseInlineHandler(ClientIdentityConsistency.ValidateAsync));
        return options;
    }

    internal static IEndpointRouteBuilder MapToken(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/token", TokenEndpoint.HandleAsync);
        return endpoints;
    }
}
