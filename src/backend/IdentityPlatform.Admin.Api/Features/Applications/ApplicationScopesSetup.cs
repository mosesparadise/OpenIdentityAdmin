using IdentityPlatform.Admin.Api.Features.ApiContracts;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Registers only the explicit versioned assignment subresource with independent read/write authentication and finite contracts.</summary>
internal static class ApplicationScopesSetup
{
    private const string Path = "/applications/{id}/scopes";
    private const string ReadOperation = "GetApplicationScopes";
    private const string WriteOperation = "AssignApplicationScopes";
    private const string ReadSummary = "Read the application's additional scope permissions and current version";
    private const string WriteSummary = "Explicitly replace scope permissions with current write permission and fresh MFA";
    private const string JsonType = "application/json";

    /// <summary>Preserves all existing editor operations and exposes typed assignment results with explicit denied/stale/unavailable outcomes.</summary>
    internal static void Map(IEndpointRouteBuilder app)
    {
        var read = app.MapManagementGroup().MapGet(Path, (HttpContext http, string id) => ApplicationScopesEndpoint.ReadAsync(http, id))
            .WithName(ReadOperation).WithSummary(ReadSummary).Produces<ApplicationScopes>();
        var write = app.MapManagementGroup().MapPut(Path, (HttpContext http, string id) => ApplicationScopesEndpoint.WriteAsync(http, id))
            .WithName(WriteOperation).WithSummary(WriteSummary).Accepts<ApplicationScopeInput>(JsonType).Produces<ApplicationScopes>()
            .ProducesProblem(StatusCodes.Status412PreconditionFailed).ProducesProblem(StatusCodes.Status428PreconditionRequired);
        foreach (var endpoint in new[] { read, write })
        {
            endpoint.ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
