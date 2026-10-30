using System.Text.Json.Nodes;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Describes only implemented create/details operations, their exact authority and noncacheable credential-safe projections.</summary>
internal sealed class ApplicationEditorContract : IOpenApiDocumentTransformer
{
    private const string Collection = "/bff/api/v1/applications";
    private const string Details = Collection + "/{id}";
    private const string CreateOperation = "CreateBrowserApplication";
    private const string DetailsOperation = "GetBrowserApplication";
    private const string Scheme = "BrowserSession";
    private const string Permission = "x-required-permission";
    private const string Read = "applications.read";
    private const string Write = "applications.write";
    private const string Cache = "Cache-Control";
    private const string CacheDescription = "Application data and generated credentials must not be cached.";
    private const string NoStore = "no-store";
    private const string Version = "ETag";
    private const string VersionDescription = "Exact strong aggregate version; absent for protected or unmanaged registrations.";
    private const string Id = "id";
    private const string IdDescription = "Exact native application identity; the BFF reserves antiforgery for its token endpoint.";
    private const string Ok = "200";
    private const string Created = "201";
    private const string Downstream = "x-downstream-api-version";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string CsrfDescription = "Native token bound to this browser generation and application mutation purpose.";
    private const int TokenCharacters = 4096;

    /// <summary>Applies metadata only to the named actual operations; list, token and dormant production inventories retain their own contracts.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        Apply(document, Collection, HttpMethod.Post, CreateOperation, create: true);
        Apply(document, Details, HttpMethod.Get, DetailsOperation, create: false);
        return Task.CompletedTask;
    }

    /// <summary>Publishes authentication, permission, exact input and success-version metadata without granting authority through documentation.</summary>
    private static void Apply(OpenApiDocument document, string path, HttpMethod method, string name, bool create)
    {
        if (!document.Paths.TryGetValue(path, out var item) || item.Operations?.TryGetValue(method, out var operation) != true ||
            operation is null || operation.OperationId != name)
        {
            return;
        }
        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(Scheme, document)] = [] }];
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions[Permission] = new JsonNodeExtension(JsonValue.Create(create ? Write : Read));
        if (!create)
        {
            operation.Parameters = [new OpenApiParameter { Name = Id, In = ParameterLocation.Path, Required = true, Description = IdDescription,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = ApplicationProjection.IdentifierLength } }];
        }
        operation.Extensions[Downstream] = new JsonNodeExtension(JsonValue.Create(1));
        if (create)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = Csrf,
                In = ParameterLocation.Header,
                Required = true,
                Description = CsrfDescription,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = TokenCharacters }
            });
        }
        foreach (var (status, entry) in operation.Responses!)
        {
            if (entry is not OpenApiResponse response)
            {
                throw new InvalidOperationException();
            }
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[Cache] = new OpenApiHeader { Description = CacheDescription, Schema = new OpenApiSchema { Type = JsonSchemaType.String, Const = NoStore } };
            if (status == (create ? Created : Ok))
            {
                response.Headers[Version] = new OpenApiHeader
                {
                    Description = VersionDescription,
                    Required = create,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                };
            }
        }
    }
}
