using IdentityPlatform.Admin.Bff.Features.ApiContracts;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Composes one explicitly configured application proxy without a generic forwarding surface.</summary>
internal static class Setup
{
    private const string DetailsPath = "/applications/{id}";
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
        return endpoints;
    }
}
