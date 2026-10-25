using System.Text.Json.Nodes;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Contracts = IdentityPlatform.Admin.Bff.Features.ApiContracts.Setup;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Maps the same dormant status handler for native metadata export; no production composition invokes this adapter.</summary>
internal static class BrowserSessionContract
{
    internal const string Operation = "ReadBrowserSession";
    internal const string Scheme = "BrowserSession";
    private const string Summary = "Read local browser session status without renewal";
    private const string Tag = "Browser session";

    /// <summary>Supplies native endpoint metadata using the actual DTO and handler while leaving login HTML and protocol responses outside OpenAPI.</summary>
    internal static void MapBrowserSessionStatus(this WebApplication app)
    {
        app.Use((http, next) => http.Request.Path == BrowserSessionBoundary.Path
            ? BrowserSessionBoundary.RunAsync(http, app.Services.GetRequiredService<IServiceScopeFactory>(), app.Services.GetRequiredService<TimeProvider>())
            : next(http));
        var versions = app.NewApiVersionSet().HasApiVersion(new ApiVersion(Contracts.SupportedMajor)).Build();
        app.MapGet(BrowserSessionBoundary.Path, (HttpContext http, [FromServices] IServiceScopeFactory factory, [FromServices] TimeProvider clock) =>
                BrowserSessionBoundary.RunAsync(http, factory, clock))
            .WithName(Operation).WithSummary(Summary).WithTags(Tag).WithGroupName(Contracts.DocumentName).WithApiVersionSet(versions).IsApiVersionNeutral()
            .Produces<BrowserSessionStatus>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status503ServiceUnavailable);
    }
}

/// <summary>Describes optional cookie authentication for one local read without weakening management permission/version contracts.</summary>
internal sealed class BrowserSessionContractTransformer : IOpenApiDocumentTransformer
{
    private const string StatusExtension = "x-browser-session-status";
    private const string CacheHeader = "Cache-Control";
    private const string NoStore = "no-store";
    private const string CacheDescription = "Local authentication responses must not be cached.";

    /// <summary>Changes only the actual status operation; health-only production exports remain empty and native callback routes remain absent.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (!document.Paths.TryGetValue(BrowserSessionBoundary.Path, out var path) ||
            path.Operations?.TryGetValue(HttpMethod.Get, out var operation) != true || operation is null || operation.OperationId != BrowserSessionContract.Operation)
        {
            return Task.CompletedTask;
        }
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[BrowserSessionContract.Scheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = BrowserSessionCookies.Session
        };
        operation.Security = [new OpenApiSecurityRequirement(), new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BrowserSessionContract.Scheme, document)] = []
        }];
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions[StatusExtension] = new JsonNodeExtension(JsonValue.Create(true));
        foreach (var response in operation.Responses!.Values.OfType<OpenApiResponse>())
        {
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[CacheHeader] = new OpenApiHeader
            {
                Description = CacheDescription,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Const = NoStore }
            };
        }
        return Task.CompletedTask;
    }
}
