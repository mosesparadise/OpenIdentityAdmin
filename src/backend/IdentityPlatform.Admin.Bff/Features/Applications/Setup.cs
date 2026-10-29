using IdentityPlatform.Admin.Bff.Features.ApiContracts;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Composes one explicitly configured application proxy without a generic forwarding surface.</summary>
internal static class Setup
{
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
        return endpoints;
    }
}
