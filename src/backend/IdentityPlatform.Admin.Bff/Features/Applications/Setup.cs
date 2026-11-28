using IdentityPlatform.Admin.Bff.Features.ApiContracts;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Composes one explicitly configured application proxy without a generic forwarding surface.</summary>
internal static class Setup
{
    private const string CloneOperation = "CloneBrowserApplication";
    private const string CloneSummary = "Clone current managed configuration into a disabled application with a new one-time secret";
    private const string DetailsPath = "/applications/{id}";
    private const string ReplacementPath = DetailsPath + "/secret-replacements";
    private const string DisableOperation = "DisableBrowserApplication";
    private const string EnableOperation = "EnableBrowserApplication";
    private const string DisableSummary = "Disable new sign-ins and issuance without revoking existing grants";
    private const string EnableSummary = "Re-enable fresh sign-ins without restoring old attempts or extending credentials";
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
    /// <summary>Offers explicit cookie-and-antiforgery routes for public-key enrollment and current-version rotation.</summary>
    private static void MapKeyActions(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapManagementGroup().MapPost(CollectionPath + MachineClientRules.KeyRegistrationSuffix, (Func<HttpContext, Task>)(context =>
            ApplicationMutationProxy.HandleAsync(context, ApplicationProxyAction.CreateMachineKey)))
            .WithName(ApplicationEditorContract.KeyMachineOperation).WithSummary(ApplicationEditorContract.KeyMachineSummary)
            .Accepts<ApplicationKeyMachineCreate>(JsonType).Produces<ApplicationDetails>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        endpoints.MapManagementGroup().MapPost(DetailsPath + MachineClientRules.KeyReplacementSuffix, (HttpContext context, string id) =>
            ApplicationMutationProxy.HandleAsync(context, ApplicationProxyAction.ReplaceKey, id))
            .WithName(ApplicationEditorContract.KeyReplacementOperation).WithSummary(ApplicationEditorContract.KeyReplacementSummary)
            .Accepts<ApplicationKeyReplacement>(JsonType).Produces<ApplicationDetails>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

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
        endpoints.MapManagementGroup().MapPost(CollectionPath + ApplicationEditorRules.NativeRegistrationSuffix, (Func<HttpContext, Task>)(context => ApplicationMutationProxy.HandleAsync(context, ApplicationProxyAction.CreateNative)))
            .WithName(ApplicationEditorContract.NativeOperation).WithSummary(ApplicationEditorContract.NativeSummary).Accepts<ApplicationNativeCreate>(JsonType)
            .Produces<ApplicationDetails>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        endpoints.MapManagementGroup().MapPost(CollectionPath + ApplicationEditorRules.BrowserRegistrationSuffix, (Func<HttpContext, Task>)(context => ApplicationMutationProxy.HandleAsync(context, ApplicationProxyAction.CreateBrowser)))
            .WithName(ApplicationEditorContract.BrowserOperation).WithSummary(ApplicationEditorContract.BrowserSummary).Accepts<ApplicationBrowserCreate>(JsonType)
            .Produces<ApplicationDetails>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        MapKeyActions(endpoints);
        endpoints.MapManagementGroup().MapPost(CollectionPath + MachineClientRules.Suffix, (Func<HttpContext, Task>)(context => ApplicationMutationProxy.HandleAsync(context, ApplicationProxyAction.CreateMachine)))
            .WithName(ApplicationEditorContract.MachineOperation).WithSummary(ApplicationEditorContract.MachineSummary).Accepts<ApplicationMachineCreate>(JsonType)
            .Produces<ApplicationCredentialOutput>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
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
        ApplicationScopesSetup.Map(endpoints);
        ApplicationRetirementSetup.Map(endpoints);
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
        var disable = endpoints.MapManagementGroup().MapPost(DetailsPath + ApplicationLifecycleRules.DisableSuffix, (HttpContext context, string id) =>
            ApplicationMutationProxy.HandleAsync(context, ApplicationProxyAction.Disable, id))
            .WithName(DisableOperation).WithSummary(DisableSummary)
            .Accepts<ApplicationLifecycle>(JsonType).Produces<ApplicationDetails>();
        var enable = endpoints.MapManagementGroup().MapPost(DetailsPath + ApplicationLifecycleRules.EnableSuffix, (HttpContext context, string id) =>
            ApplicationMutationProxy.HandleAsync(context, ApplicationProxyAction.Enable, id))
            .WithName(EnableOperation).WithSummary(EnableSummary)
            .Accepts<ApplicationLifecycle>(JsonType).Produces<ApplicationDetails>();
        var clone = endpoints.MapManagementGroup().MapPost(DetailsPath + ApplicationCloneRules.Suffix, (HttpContext context, string id) =>
            ApplicationMutationProxy.HandleAsync(context, ApplicationProxyAction.Clone, id))
            .WithName(CloneOperation).WithSummary(CloneSummary).Accepts<ApplicationClone>(JsonType)
            .Produces<ApplicationCredentialOutput>(StatusCodes.Status201Created);
        foreach (var endpoint in new[] { edit, replace, disable, enable, clone })
        {
            endpoint.ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status412PreconditionFailed)
                .ProducesProblem(StatusCodes.Status428PreconditionRequired).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
