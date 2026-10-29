using IdentityPlatform.Admin.Api.Features.ApiContracts;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Publishes only the explicitly selected management collection version; protocol routes remain separate.</summary>
internal static class Setup
{
    private const string CollectionPath = "/applications";
    private const string Operation = "ListApplications";
    private const string Summary = "List registered applications";
    /// <summary>Attaches authoritative typed metadata to the actual independently authenticated endpoint.</summary>
    internal static IEndpointRouteBuilder MapApplicationList(this IEndpointRouteBuilder app)
    {
        app.MapManagementGroup().MapGet(CollectionPath, (Func<HttpContext, Task>)ApplicationListEndpoint.HandleAsync)
            .WithName(Operation).WithSummary(Summary)
            .Produces<ApplicationPage>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }
}
