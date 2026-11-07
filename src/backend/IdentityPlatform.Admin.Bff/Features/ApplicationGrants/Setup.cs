using IdentityPlatform.Admin.Bff.Features.ApiContracts;
using IdentityPlatform.Core.Features.ApplicationGrants;

namespace IdentityPlatform.Admin.Bff.Features.ApplicationGrants;

/// <summary>Maps explicit same-origin grant endpoints without exposing a generic forwarding route.</summary>
internal static class Setup
{
    private const string CreateOperation = "CreateBrowserApplicationGrantRevocation";
    private const string CreateSummary = "Confirm durable revocation while keeping the application disabled";
    private const string ContinueOperation = "ContinueBrowserApplicationGrantRevocation";
    private const string ContinueSummary = "Freshly confirm remaining work in a stopped revocation job";
    private const string ListOperation = "ListBrowserApplicationGrantRevocations";
    private const string ListSummary = "Read the latest ten revocation jobs using current permissions";
    private const string GetOperation = "GetBrowserApplicationGrantRevocation";
    private const string GetSummary = "Read committed revocation progress using current permissions";

    private const string Collection = "/applications/{id}/grant-revocations";
    private const string Job = Collection + "/{jobId}";
    private const string JsonType = "application/json";

    /// <summary>Publishes the actual browser projection and distinct mutation preconditions for each closed operation.</summary>
    internal static IEndpointRouteBuilder MapGrantProxy(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapManagementGroup();
        var submit = group.MapPost(Collection, (HttpContext http, string id) => GrantProxy.HandleAsync(http, id, null, true))
            .WithName(CreateOperation).WithSummary(CreateSummary)
            .Accepts<GrantRevocation>(JsonType).Produces<GrantAcceptance>(StatusCodes.Status202Accepted);
        var resume = group.MapPost(Job + GrantRevocationRules.ContinueSuffix, (HttpContext http, string id, string jobId) => GrantProxy.HandleAsync(http, id, jobId, true))
            .WithName(ContinueOperation).WithSummary(ContinueSummary)
            .Accepts<GrantRevocation>(JsonType).Produces<GrantAcceptance>(StatusCodes.Status202Accepted);
        var recent = group.MapGet(Collection, (HttpContext http, string id) => GrantProxy.HandleAsync(http, id, null, false))
            .WithName(ListOperation).WithSummary(ListSummary).Produces<GrantJobPage>();
        var read = group.MapGet(Job, (HttpContext http, string id, string jobId) => GrantProxy.HandleAsync(http, id, jobId, false))
            .WithName(GetOperation).WithSummary(GetSummary).Produces<GrantJobView>();
        foreach (var endpoint in new[] { submit, resume, recent, read })
        {
            endpoint.ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        }
        foreach (var endpoint in new[] { submit, resume })
        {
            endpoint.ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status412PreconditionFailed).ProducesProblem(StatusCodes.Status428PreconditionRequired);
        }
        return endpoints;
    }
}
