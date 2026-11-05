using IdentityPlatform.Admin.Api.Features.ApiContracts;
using IdentityPlatform.Core.Features.ApplicationGrants;

namespace IdentityPlatform.Admin.Api.Features.ApplicationGrants;

/// <summary>Maps the four closed versioned job operations with explicit response/error contracts.</summary>
internal static class Setup
{
    private const string CreateOperation = "CreateApplicationGrantRevocation";
    private const string CreateSummary = "Accept durable revocation for an already disabled managed application";
    private const string ContinueOperation = "ContinueApplicationGrantRevocation";
    private const string ContinueSummary = "Explicitly approve continuation of stopped revocation with fresh MFA";
    private const string ListOperation = "ListApplicationGrantRevocations";
    private const string ListSummary = "Read up to ten recent jobs with current application-read and revocation permissions";
    private const string GetOperation = "GetApplicationGrantRevocation";
    private const string GetSummary = "Read committed revocation progress with current permissions";

    private const string Collection = "/applications/{id}/grant-revocations";
    private const string Job = Collection + "/{jobId}";
    private const string JsonType = "application/json";

    /// <summary>Keeps grant operations within explicitly activated management, separate from protocol endpoints.</summary>
    internal static IEndpointRouteBuilder MapGrantRevocations(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapManagementGroup();
        var submit = group.MapPost(Collection, (HttpContext http, string id) => GrantEndpoint.WriteAsync(http, id))
            .WithName(CreateOperation).WithSummary(CreateSummary)
            .Accepts<GrantRevocation>(JsonType).Produces<GrantAcceptance>(StatusCodes.Status202Accepted);
        var resume = group.MapPost(Job + GrantRevocationRules.ContinueSuffix, (HttpContext http, string id, string jobId) => GrantEndpoint.WriteAsync(http, id, jobId))
            .WithName(ContinueOperation).WithSummary(ContinueSummary)
            .Accepts<GrantRevocation>(JsonType).Produces<GrantAcceptance>(StatusCodes.Status202Accepted);
        var recent = group.MapGet(Collection, (HttpContext http, string id) => GrantEndpoint.ReadAsync(http, id))
            .WithName(ListOperation).WithSummary(ListSummary).Produces<GrantJobPage>();
        var read = group.MapGet(Job, (HttpContext http, string id, string jobId) => GrantEndpoint.ReadAsync(http, id, jobId))
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
