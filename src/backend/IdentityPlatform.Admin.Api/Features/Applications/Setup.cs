using IdentityPlatform.Admin.Api.Features.ApiContracts;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Publishes the explicitly selected collection, create and details version; protocol routes remain separate.</summary>
internal static class Setup
{
    private const string DetailsPath = "/applications/{id}";
    private const string ReplacementPath = DetailsPath + "/secret-replacements";
    private const string DisableOperation = "DisableApplication";
    private const string EnableOperation = "EnableApplication";
    private const string DisableSummary = "Disable new sign-ins and issuance without revoking existing grants";
    private const string EnableSummary = "Re-enable fresh sign-ins without restoring old attempts or extending credentials";
    private const string EditOperation = "EditApplication";
    private const string ReplacementOperation = "ReplaceApplicationSecret";
    private const string EditSummary = "Edit application presentation and callbacks using its current strong version";
    private const string ReplacementSummary = "Replace a managed secret once with current credential permission and fresh MFA";
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
        MapUpdates(app);
        return app;
    }

    /// <summary>Maps only the finite version-bound edit and credential actions with their complete public outcomes.</summary>
    private static void MapUpdates(IEndpointRouteBuilder endpoints)
    {
        var edit = endpoints.MapManagementGroup().MapPut(DetailsPath, (HttpContext context, string id) =>
            ApplicationUpdateEndpoint.HandleAsync(context, id, ApplicationMutationKind.Edit))
            .WithName(EditOperation).WithSummary(EditSummary).Accepts<ApplicationEdit>(JsonType).Produces<ApplicationDetails>();
        var replace = endpoints.MapManagementGroup().MapPost(ReplacementPath, (HttpContext context, string id) =>
            ApplicationUpdateEndpoint.HandleAsync(context, id, ApplicationMutationKind.Replace))
            .WithName(ReplacementOperation).WithSummary(ReplacementSummary).Accepts<ApplicationSecretReplacement>(JsonType).Produces<ApplicationCredentialOutput>();
        var disable = endpoints.MapManagementGroup().MapPost(DetailsPath + ApplicationLifecycleRules.DisableSuffix, (HttpContext context, string id) =>
            ApplicationUpdateEndpoint.HandleAsync(context, id, ApplicationMutationKind.Disable))
            .WithName(DisableOperation).WithSummary(DisableSummary)
            .Accepts<ApplicationLifecycle>(JsonType).Produces<ApplicationDetails>();
        var enable = endpoints.MapManagementGroup().MapPost(DetailsPath + ApplicationLifecycleRules.EnableSuffix, (HttpContext context, string id) =>
            ApplicationUpdateEndpoint.HandleAsync(context, id, ApplicationMutationKind.Enable))
            .WithName(EnableOperation).WithSummary(EnableSummary)
            .Accepts<ApplicationLifecycle>(JsonType).Produces<ApplicationDetails>();
        foreach (var endpoint in new[] { edit, replace, disable, enable })
        {
            endpoint.ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status412PreconditionFailed)
                .ProducesProblem(StatusCodes.Status428PreconditionRequired).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
