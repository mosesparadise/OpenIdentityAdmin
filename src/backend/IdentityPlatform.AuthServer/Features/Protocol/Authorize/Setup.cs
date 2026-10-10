namespace IdentityPlatform.AuthServer.Features.Protocol.Authorize;

internal static class Setup
{
    internal static IEndpointRouteBuilder MapAuthorize(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/connect/authorize", ["GET", "POST"], AuthorizeEndpoint.HandleAsync);
        return endpoints;
    }
}
