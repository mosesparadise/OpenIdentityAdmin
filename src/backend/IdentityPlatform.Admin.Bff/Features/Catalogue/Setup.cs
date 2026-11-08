using IdentityPlatform.Admin.Bff.Features.ApiContracts;
using IdentityPlatform.Core.Features.Catalogue;

namespace IdentityPlatform.Admin.Bff.Features.Catalogue;

/// <summary>Maps only the two fixed versioned catalogue surfaces with cookie-bound, same-origin operations.</summary>
internal static class Setup
{
    private const string DetailSuffix = "/{id}";
    private const string JobSuffix = "/{jobId}";
    private const string JsonType = "application/json";
    private const string List = "ListBrowser";
    private const string Get = "GetBrowser";
    private const string Create = "CreateBrowser";
    private const string Edit = "EditBrowser";
    private const string Delete = "DeleteBrowser";
    private const string TokenOperation = "GetCatalogueAntiforgery";
    private const string Deletion = "Deletion";
    private const string ScopeName = "Scope";
    private const string AudienceName = "Audience";
    private const string Plural = "s";

    /// <summary>Activates the catalogue alongside the already explicitly enabled browser profile.</summary>
    internal static IEndpointRouteBuilder MapCatalogueProxy(this IEndpointRouteBuilder endpoints)
    {
        Map(endpoints, CatalogueVersion.Scope, CataloguePaths.Scopes, ScopeName);
        Map(endpoints, CatalogueVersion.Audience, CataloguePaths.Audiences, AudienceName);
        return endpoints;
    }

    /// <summary>Attaches typed native OpenAPI metadata to actual finite handlers without introducing a generic route forwarder.</summary>
    private static void Map(IEndpointRouteBuilder endpoints, string kind, string path, string name)
    {
        var group = endpoints.MapManagementGroup();
        group.MapGet(path + CataloguePaths.Antiforgery, (Func<HttpContext, Task>)(http => CatalogueAntiforgeryEndpoint.HandleAsync(http, kind)))
            .WithName(TokenOperation + name).Produces<CatalogueAntiforgeryToken>().ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        var list = group.MapGet(path, (Func<HttpContext, Task>)(http => CatalogueProxy.HandleAsync(http, kind, CatalogueProxyAction.List)))
            .WithName(List + name + Plural).Produces<CataloguePage>();
        var get = group.MapGet(path + DetailSuffix, (HttpContext http, string id) => CatalogueProxy.HandleAsync(http, kind, CatalogueProxyAction.Details, id))
            .WithName(Get + name);
        var create = group.MapPost(path, (Func<HttpContext, Task>)(http => CatalogueProxy.HandleAsync(http, kind, CatalogueProxyAction.Create)))
            .WithName(Create + name);
        var edit = group.MapPut(path + DetailSuffix, (HttpContext http, string id) => CatalogueProxy.HandleAsync(http, kind, CatalogueProxyAction.Edit, id))
            .WithName(Edit + name);
        var delete = group.MapDelete(path + DetailSuffix, (HttpContext http, string id) => CatalogueProxy.HandleAsync(http, kind, CatalogueProxyAction.Delete, id))
            .WithName(Delete + name).Accepts<CatalogueDeleteInput>(JsonType).Produces<CatalogueDeletionDetails>(StatusCodes.Status202Accepted);
        var job = group.MapGet(path + CataloguePaths.Deletions + JobSuffix, (HttpContext http, string jobId) => CatalogueProxy.HandleAsync(http, kind, CatalogueProxyAction.Status, jobId: jobId))
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
    private static void Details(string kind, RouteHandlerBuilder get, RouteHandlerBuilder create, RouteHandlerBuilder edit)
    {
        if (kind == CatalogueVersion.Scope)
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
