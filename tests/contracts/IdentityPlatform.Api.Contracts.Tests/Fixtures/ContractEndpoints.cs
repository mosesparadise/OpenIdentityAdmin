using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ApiContracts = IdentityPlatform.Admin.Api.Features.ApiContracts.Setup;
using BffContracts = IdentityPlatform.Admin.Bff.Features.ApiContracts.Setup;

namespace IdentityPlatform.Api.Contracts.Tests.Fixtures;

/// <summary>Maps synthetic operations that exercise native schema/version tooling without supplying production authentication.</summary>
internal static class ContractEndpoints
{
    private const string ContractTag = "IdentityPlatform.Api.Contracts.Tests";
    private const string AlivePath = "/alive";
    private const string Healthy = "Healthy";
    private const string ListOperation = "ListExamples";
    private const string GetOperation = "GetExample";
    private const string UpdateOperation = "UpdateExample";
    private const string ListSummary = "List synthetic examples";
    private const string GetSummary = "Get a synthetic example";
    private const string UpdateSummary = "Update a synthetic example";

    /// <summary>Compose isolated sample operations and dormant renderers, proxying only the fixed upstream contract path.</summary>
    internal static void Map(WebApplication app, bool browserSurface, ContractHost? upstream)
    {
        var group = browserSurface ? BffContracts.MapManagementGroup(app) : ApiContracts.MapManagementGroup(app);
        if (browserSurface)
        {
            group.AddEndpointFilter(CheckFixtureProofAsync);
        }
        group.WithTags(ContractTag);
        MapExamples(group);
        app.MapGet(AlivePath, () => Healthy);
        if (upstream is not null)
        {
            app.MapGet(ContractValues.ApiDocument, async (CancellationToken cancellationToken) => Results.Content(
                await upstream.Client.GetStringAsync(ContractValues.ApiDocument, cancellationToken), ContractValues.JsonMediaType)).ExcludeFromDescription();
        }
        if (browserSurface)
        {
            IdentityPlatform.Admin.Bff.Features.Documentation.Setup.MapDocumentation(app);
        }
        else
        {
            IdentityPlatform.Admin.Api.Features.Documentation.Setup.MapDocumentation(app);
        }
    }

    /// <summary>Exercise consumer cookie/proof transport with synthetic credentials; this filter is never a product security control.</summary>
    private static async ValueTask<object?> CheckFixtureProofAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.Cookies[ContractValues.CookieName] != ContractValues.CookieValue)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized);
        }
        if (HttpMethods.IsPut(request.Method) && request.Headers[ContractValues.ProofHeader] != ContractValues.Proof)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden);
        }
        return await next(context);
    }

    /// <summary>Describe sample input bounds and all existing documented outcomes through native endpoint metadata.</summary>
    private static void MapExamples(RouteGroupBuilder group)
    {
        group.MapGet(ContractValues.ExamplePath, ListExamples).WithName(ListOperation).WithSummary(ListSummary)
            .Produces<ExamplePage>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden);
        group.MapGet(ContractValues.ExampleById, GetExample).WithName(GetOperation).WithSummary(GetSummary)
            .Produces<Example>().ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPut(ContractValues.ExampleById, UpdateExample).WithName(UpdateOperation).WithSummary(UpdateSummary)
            .Produces<Example>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status412PreconditionFailed).ProducesProblem(StatusCodes.Status428PreconditionRequired);
    }

    /// <summary>Return a bounded synthetic page or the same documented bad-request outcome.</summary>
    private static IResult ListExamples([FromQuery, Range(1, ContractValues.MaximumPage)] int pageSize = ContractValues.DefaultPage) =>
        pageSize is < 1 or > ContractValues.MaximumPage ? Results.Problem(statusCode: StatusCodes.Status400BadRequest) :
            Results.Ok(new ExamplePage([new(ContractValues.ExampleId, null, ExampleState.Active, DateTimeOffset.UnixEpoch)], pageSize));

    /// <summary>Expose the synthetic edit version and corresponding example.</summary>
    private static Example GetExample(string id, HttpContext context)
    {
        context.Response.Headers.ETag = ContractValues.Version;
        return new Example(id, null, ExampleState.Active, DateTimeOffset.UnixEpoch);
    }

    /// <summary>Require the synthetic precondition header before producing an updated sample.</summary>
    private static IResult UpdateExample(string id, ExampleUpdate update, HttpContext context) =>
        !context.Request.Headers.ContainsKey(ContractValues.IfMatchHeader) ? Results.Problem(statusCode: StatusCodes.Status428PreconditionRequired) :
            Results.Ok(new Example(id, update.Label, ExampleState.Active, DateTimeOffset.UnixEpoch));
}
