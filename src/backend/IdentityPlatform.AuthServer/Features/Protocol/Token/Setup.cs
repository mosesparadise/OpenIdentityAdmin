namespace IdentityPlatform.AuthServer.Features.Protocol.Token;

internal static class Setup
{
    internal static IEndpointRouteBuilder MapToken(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/token", TokenEndpoint.HandleAsync);
        return endpoints;
    }
}
