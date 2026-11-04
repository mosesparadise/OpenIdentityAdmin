using System.Text.Json.Nodes;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Describes implemented application editor operations, their exact authority and noncacheable credential-safe projections.</summary>
internal sealed class ApplicationEditorContract : IOpenApiDocumentTransformer
{
    private const string Collection = "/bff/api/v1/applications";
    private const string Details = Collection + "/{id}";
    private const string Replacement = Details + "/secret-replacements";
    private const string EditOperation = "EditBrowserApplication";
    private const string ReplacementOperation = "ReplaceBrowserApplicationSecret";
    private const string Credentials = "applications.credentials";
    private const string FreshMfa = "x-persisted-mfa-max-age-exclusive-seconds";
    private const int MfaSeconds = 300;
    private const string IfMatch = "If-Match";
    private const string PreconditionDescription = "One canonical strong resource-bound ETag from the latest read or mutation; missing is 428, malformed or stale is 412.";
    private const int VersionMinimum = 68;
    private const int VersionMaximum = 86;
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
        Apply(document, Collection, HttpMethod.Post, CreateOperation);
        Apply(document, Details, HttpMethod.Get, DetailsOperation);
        Apply(document, Details, HttpMethod.Put, EditOperation);
        Apply(document, Replacement, HttpMethod.Post, ReplacementOperation);
        return Task.CompletedTask;
    }

    /// <summary>Publishes authentication, permission, exact input and success-version metadata without granting authority through documentation.</summary>
    private static void Apply(OpenApiDocument document, string path, HttpMethod method, string name)
    {
        if (!document.Paths.TryGetValue(path, out var item) || item.Operations?.TryGetValue(method, out var operation) != true ||
            operation is null || operation.OperationId != name)
        {
            return;
        }
        var create = path == Collection;
        var read = method == HttpMethod.Get;
        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(Scheme, document)] = [] }];
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions[Permission] = new JsonNodeExtension(JsonValue.Create(path == Replacement ? Credentials : read ? Read : Write));
        if (path == Replacement)
        {
            operation.Extensions[FreshMfa] = new JsonNodeExtension(JsonValue.Create(MfaSeconds));
        }
        operation.Extensions[Downstream] = new JsonNodeExtension(JsonValue.Create(1));
        Parameters(operation, create, read);
        Responses(operation, create, read);
    }

    /// <summary>Documents exact resource identity, mandatory update precondition and each native browser mutation proof.</summary>
    private static void Parameters(OpenApiOperation operation, bool create, bool read)
    {
        operation.Parameters = [];
        if (!create)
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = Id,
                In = ParameterLocation.Path,
                Required = true,
                Description = IdDescription,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = ApplicationProjection.IdentifierLength }
            });
        }
        if (!create && !read)
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = IfMatch,
                In = ParameterLocation.Header,
                Required = true,
                Description = PreconditionDescription,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = VersionMinimum, MaxLength = VersionMaximum }
            });
        }
        if (!read)
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = Csrf,
                In = ParameterLocation.Header,
                Required = true,
                Description = CsrfDescription,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = TokenCharacters }
            });
        }
    }

    /// <summary>Requires no-store for every outcome and a strong version for every committed mutation.</summary>
    private static void Responses(OpenApiOperation operation, bool create, bool read)
    {
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
                    Required = !read,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                };
            }
        }
    }
}
