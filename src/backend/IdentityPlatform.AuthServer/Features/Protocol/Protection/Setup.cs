using OpenIddict.Server.AspNetCore;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityPlatform.AuthServer.Features.Protocol.Protection;

internal static class Setup
{
    internal static OpenIddictServerBuilder AddFormExtractionProtection(this OpenIddictServerBuilder options)
    {
        options.AddEventHandler<ExtractAuthorizationRequestContext>(handler => handler
            .AddFilter<OpenIddictServerAspNetCoreHandlerFilters.RequireHttpRequest>()
            .SetOrder(OpenIddictServerAspNetCoreHandlers.ExtractGetOrPostRequest<ExtractAuthorizationRequestContext>.Descriptor.Order - 1)
            .UseInlineHandler(FormMediaType.ValidateAsync));
        options.AddEventHandler<ExtractTokenRequestContext>(handler => handler
            .AddFilter<OpenIddictServerAspNetCoreHandlerFilters.RequireHttpRequest>()
            .SetOrder(OpenIddictServerAspNetCoreHandlers.ExtractPostRequest<ExtractTokenRequestContext>.Descriptor.Order - 1)
            .UseInlineHandler(FormMediaType.ValidateAsync));
        return options;
    }

}
