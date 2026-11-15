using IdentityPlatform.Admin.Bff.Features.ApiContracts;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Publishes the fixed same-origin retirement mutation separately from ordinary lifecycle and opaque detail paths.</summary>
internal static class ApplicationRetirementSetup
{
    private const string ListOperation = "ListRetiredBrowserApplications";
    private const string ListSummary = "List permanently retired applications with retained history";

    private const string Path = "/applications/{id}/retirement";
    private const string Operation = "RetireBrowserApplication";
    private const string Summary = "Confirm permanent retirement and durable revocation while retaining history";
    private const string JsonType = "application/json";

    /// <summary>Describes the actual bounded acceptance and finite independent cookie/CSRF/authority/precondition failures.</summary>
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapManagementGroup().MapGet(ApplicationRetirementRules.ListPath, (Func<HttpContext, Task>)ApplicationProxyEndpoint.HandleRetiredAsync)
            .WithName(ListOperation).WithSummary(ListSummary)
            .Produces<ApplicationPage>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        endpoints.MapManagementGroup()
        .MapPost(Path, (HttpContext http, string id) => ApplicationRetirementProxy.HandleAsync(http, id))
        .WithName(Operation).WithSummary(Summary).Accepts<ApplicationLifecycle>(JsonType)
        .Produces<ApplicationRetirementAcceptance>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status428PreconditionRequired).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}
