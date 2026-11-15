using IdentityPlatform.Admin.Api.Features.ApiContracts;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Maps terminal admission separately from ordinary lifecycle mutation and historical opaque application identifiers.</summary>
internal static class ApplicationRetirementSetup
{
    private const string ListOperation = "ListRetiredApplications";
    private const string ListSummary = "List permanently retired applications with retained history";

    private const string Path = "/applications/{id}/retirement";
    private const string Operation = "RetireApplication";
    private const string Summary = "Permanently retire a managed application and admit durable grant revocation";
    private const string JsonType = "application/json";

    /// <summary>Publishes the actual confirmed retirement response and finite conflict/precondition vocabulary.</summary>
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapManagementGroup().MapGet(ApplicationRetirementRules.ListPath, (Func<HttpContext, Task>)ApplicationListEndpoint.HandleRetiredAsync)
            .WithName(ListOperation).WithSummary(ListSummary)
            .Produces<ApplicationPage>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        endpoints.MapManagementGroup()
        .MapPost(Path, (HttpContext http, string id) => ApplicationRetirementEndpoint.HandleAsync(http, id))
        .WithName(Operation).WithSummary(Summary).Accepts<ApplicationLifecycle>(JsonType)
        .Produces<ApplicationRetirementAcceptance>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}
