using IdentityPlatform.Admin.Bff.Features.ApiContracts;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Composes one explicitly configured application proxy without a generic forwarding surface.</summary>
internal static class Setup
{
    private const string DetailsPath = "/applications/{id}";
    private const string ReplacementPath = DetailsPath + "/secret-replacements";
    private const string EditOperation = "EditBrowserApplication";
    private const string ReplacementOperation = "ReplaceBrowserApplicationSecret";
    private const string EditSummary = "Edit application presentation and callbacks using its current strong version";
    private const string ReplacementSummary = "Replace a managed secret once with current credential permission and fresh MFA";
    private const string CreateOperation = "CreateBrowserApplication";
    private const string DetailsOperation = "GetBrowserApplication";
    private const string CreateSummary = "Create a managed application and return its generated secret once";
    private const string DetailsSummary = "Read sanitized application details";
    private const string JsonType = "application/json";

    private const string TokenPath = "/applications/antiforgery";
    private const string TokenOperation = "GetApplicationAntiforgeryToken";
    private const string TokenSummary = "Acquire a native token for this browser generation's application mutations";

    private const string CollectionPath = "/applications";
    private const string Operation = "ListBrowserApplications";
    private const string Summary = "List applications through the current browser session";
    /// <summary>Owns a single fixed HTTPS transport until host disposal; no caller controls its destination or credential source.</summary>
    internal static IServiceCollection AddApplicationProxy(this IServiceCollection services, Uri api) =>
        services.AddSingleton(_ => new ApplicationProxyTransport(api));

    /// <summary>Publishes the separate browser major and safe projection metadata through native endpoint version selection.</summary>
    internal static IEndpointRouteBuilder MapApplicationProxy(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapManagementGroup().MapGet(CollectionPath, (Func<HttpContext, Task>)ApplicationProxyEndpoint.HandleAsync)
            .WithName(Operation).WithSummary(Summary)
            .Produces<ApplicationPage>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        endpoints.MapManagementGroup().MapGet(TokenPath, (Func<HttpContext, Task>)ApplicationAntiforgeryEndpoint.HandleAsync)
            .WithName(TokenOperation).WithSummary(TokenSummary)
            .Produces<ApplicationAntiforgeryToken>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        endpoints.MapManagementGroup().MapPost(CollectionPath, (Func<HttpContext, Task>)(context => ApplicationMutationProxy.HandleAsync(context, ApplicationProxyAction.Create)))
            .WithName(CreateOperation).WithSummary(CreateSummary).Accepts<ApplicationCreate>(JsonType)
            .Produces<ApplicationCredentialOutput>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        endpoints.MapManagementGroup().MapGet(DetailsPath, (HttpContext context, string id) => ApplicationDetailsProxy.HandleAsync(context, id))
            .WithName(DetailsOperation).WithSummary(DetailsSummary).Produces<ApplicationDetails>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        MapUpdates(endpoints);
        return endpoints;
    }

    /// <summary>Maps only the finite version-bound edit and credential actions with their complete public outcomes.</summary>
    private static void MapUpdates(IEndpointRouteBuilder endpoints)
    {
        var edit = endpoints.MapManagementGroup().MapPut(DetailsPath, (HttpContext context, string id) =>
            ApplicationMutationProxy.HandleAsync(context, ApplicationProxyAction.Edit, id))
            .WithName(EditOperation).WithSummary(EditSummary).Accepts<ApplicationEdit>(JsonType).Produces<ApplicationDetails>();
        var replace = endpoints.MapManagementGroup().MapPost(ReplacementPath, (HttpContext context, string id) =>
            ApplicationMutationProxy.HandleAsync(context, ApplicationProxyAction.Replace, id))
            .WithName(ReplacementOperation).WithSummary(ReplacementSummary).Accepts<ApplicationSecretReplacement>(JsonType).Produces<ApplicationCredentialOutput>();
        foreach (var endpoint in new[] { edit, replace })
        {
            endpoint.ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status412PreconditionFailed)
                .ProducesProblem(StatusCodes.Status428PreconditionRequired).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
