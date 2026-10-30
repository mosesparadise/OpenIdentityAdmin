using IdentityPlatform.Admin.Api.Features.ApiContracts;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Publishes the explicitly selected collection, create and details version; protocol routes remain separate.</summary>
internal static class Setup
{
    private const string DetailsPath = "/applications/{id}";
    private const string CreateOperation = "CreateApplication";
    private const string DetailsOperation = "GetApplication";
    private const string CreateSummary = "Create a managed application and return its generated secret once";
    private const string DetailsSummary = "Read sanitized application details";
    private const string JsonType = "application/json";

    private const string CollectionPath = "/applications";
    private const string Operation = "ListApplications";
    private const string Summary = "List registered applications";
    /// <summary>Attaches authoritative typed metadata to the actual independently authenticated endpoint.</summary>
    internal static IEndpointRouteBuilder MapApplications(this IEndpointRouteBuilder app)
    {
        app.MapManagementGroup().MapGet(CollectionPath, (Func<HttpContext, Task>)ApplicationListEndpoint.HandleAsync)
            .WithName(Operation).WithSummary(Summary)
            .Produces<ApplicationPage>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapManagementGroup().MapPost(CollectionPath, (Func<HttpContext, Task>)ApplicationCreateEndpoint.HandleAsync)
            .WithName(CreateOperation).WithSummary(CreateSummary).Accepts<ApplicationCreate>(JsonType)
            .Produces<ApplicationCredentialOutput>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapManagementGroup().MapGet(DetailsPath, (HttpContext context, string id) => ApplicationDetailsEndpoint.HandleAsync(context, id))
            .WithName(DetailsOperation).WithSummary(DetailsSummary).Produces<ApplicationDetails>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }
}
