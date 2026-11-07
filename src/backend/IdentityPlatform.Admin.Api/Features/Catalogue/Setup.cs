using IdentityPlatform.Admin.Api.Features.ApiContracts;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;

namespace IdentityPlatform.Admin.Api.Features.Catalogue;

/// <summary>Maps only the two fixed versioned catalogue surfaces with independently authenticated route-owned operations.</summary>
internal static class Setup
{
    private const string DetailSuffix = "/{id}";
    private const string JobSuffix = "/{jobId}";
    private const string JsonType = "application/json";
    private const string List = "List";
    private const string Get = "Get";
    private const string Create = "Create";
    private const string Edit = "Edit";
    private const string Delete = "Delete";
    private const string Deletion = "Deletion";
    private const string ScopeName = "Scope";
    private const string AudienceName = "Audience";
    private const string Plural = "s";

    /// <summary>Activates the catalogue alongside the already explicitly enabled management API profile.</summary>
    internal static IEndpointRouteBuilder MapCatalogue(this IEndpointRouteBuilder endpoints)
    {
        Map(endpoints, CatalogueResource.Scope, CataloguePaths.Scopes, ScopeName);
        Map(endpoints, CatalogueResource.Audience, CataloguePaths.Audiences, AudienceName);
        return endpoints;
    }

    /// <summary>Attaches typed native OpenAPI metadata to actual finite handlers without introducing a generic route forwarder.</summary>
    private static void Map(IEndpointRouteBuilder endpoints, CatalogueResource kind, string path, string name)
    {
        var group = endpoints.MapManagementGroup();
        var list = group.MapGet(path, (Func<HttpContext, Task>)(http => CatalogueReadEndpoint.HandleAsync(http, kind)))
            .WithName(List + name + Plural).Produces<CataloguePage>();
        var get = group.MapGet(path + DetailSuffix, (HttpContext http, string id) => CatalogueReadEndpoint.HandleAsync(http, kind, id))
            .WithName(Get + name);
        var create = group.MapPost(path, (Func<HttpContext, Task>)(http => CatalogueMutationEndpoint.HandleAsync(http, kind)))
            .WithName(Create + name);
        var edit = group.MapPut(path + DetailSuffix, (HttpContext http, string id) => CatalogueMutationEndpoint.HandleAsync(http, kind, id))
            .WithName(Edit + name);
        var delete = group.MapDelete(path + DetailSuffix, (HttpContext http, string id) => CatalogueMutationEndpoint.HandleAsync(http, kind, id, delete: true))
            .WithName(Delete + name).Accepts<CatalogueDeleteInput>(JsonType).Produces<CatalogueDeletionDetails>(StatusCodes.Status202Accepted);
        var job = group.MapGet(path + CataloguePaths.Deletions + JobSuffix, (HttpContext http, string jobId) => CatalogueReadEndpoint.HandleAsync(http, kind, jobId: jobId))
            .WithName(Get + name + Deletion).Produces<CatalogueDeletionDetails>();
        Details(kind, get, create, edit);
        foreach (var endpoint in new[] { list, get, create, edit, delete, job })
        {
            endpoint.ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        }
        foreach (var endpoint in new[] { edit, delete })
        {
            endpoint.ProducesProblem(StatusCodes.Status412PreconditionFailed).ProducesProblem(StatusCodes.Status428PreconditionRequired);
        }
    }

    /// <summary>Declares separate scope/audience input and response schemas rather than a permissive union property bag.</summary>
    private static void Details(CatalogueResource kind, RouteHandlerBuilder get, RouteHandlerBuilder create, RouteHandlerBuilder edit)
    {
        if (kind == CatalogueResource.Scope)
        {
            get.Produces<ScopeDetails>();
            create.Accepts<ScopeInput>(JsonType).Produces<ScopeDetails>(StatusCodes.Status201Created);
            edit.Accepts<ScopeInput>(JsonType).Produces<ScopeDetails>();
        }
        else
        {
            get.Produces<AudienceDetails>();
            create.Accepts<AudienceInput>(JsonType).Produces<AudienceDetails>(StatusCodes.Status201Created);
            edit.Accepts<AudienceInput>(JsonType).Produces<AudienceDetails>();
        }
    }
}
