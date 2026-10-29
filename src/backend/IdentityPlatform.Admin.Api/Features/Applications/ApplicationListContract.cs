using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Describes the actual collection's independent security, finite query and no-store response contract.</summary>
internal sealed class ApplicationListContract : IOpenApiDocumentTransformer
{
    private const string Bearer = "bearer";
    private const string TokenFormat = "JWT";
    private const string PermissionExtension = "x-required-permission";
    private const string Permission = "applications.read";
    private const string SearchParameter = "search";
    private const string SearchDescription = "Literal case-sensitive client-ID prefix.";
    private const string PageParameter = "page";
    private const string SizeParameter = "pageSize";
    private const string MaximumPage = "10000";
    private const string MaximumSize = "100";
    private const string CacheControl = "Cache-Control";
    private const string CacheDescription = "Application data must not be cached.";
    private const string NoStore = "no-store";
    private const string IntegerFormat = "int32";
    private const string Minimum = "1";
    private const int SearchLimit = 100;
    private const int DefaultSize = 25;
    private const string Path = "/api/v1/applications";
    private const string Operation = "ListApplications";
    private const string Scheme = "AdminBearer";

    /// <summary>Changes only this real collection operation; dormant inventories and unrelated contracts remain untouched.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (!document.Paths.TryGetValue(Path, out var path) || path.Operations?.TryGetValue(HttpMethod.Get, out var operation) != true ||
            operation is null || operation.OperationId != Operation)
        {
            return Task.CompletedTask;
        }
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[Scheme] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = Bearer, BearerFormat = TokenFormat };
        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(Scheme, document)] = [] }];
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions[PermissionExtension] = new JsonNodeExtension(JsonValue.Create(Permission));

        operation.Parameters =
        [
            new OpenApiParameter { Name = SearchParameter, In = ParameterLocation.Query, Description = SearchDescription, Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = SearchLimit } },
            Number(PageParameter, MaximumPage, 1), Number(SizeParameter, MaximumSize, DefaultSize)
        ];
        foreach (var response in operation.Responses!.Values.OfType<OpenApiResponse>())
        {
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[CacheControl] = new OpenApiHeader { Description = CacheDescription, Schema = new OpenApiSchema { Type = JsonSchemaType.String, Const = NoStore } };
        }
        return Task.CompletedTask;
    }

    /// <summary>Publishes explicit positive pagination bounds and defaults without silently accepting alternate textual forms.</summary>
    private static OpenApiParameter Number(string name, string maximum, int fallback) => new()
    {
        Name = name,
        In = ParameterLocation.Query,
        Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = IntegerFormat, Minimum = Minimum, Maximum = maximum, Default = JsonValue.Create(fallback) }
    };
}
