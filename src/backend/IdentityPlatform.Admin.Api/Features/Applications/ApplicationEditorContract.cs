using System.Text.Json.Nodes;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Describes implemented application editor operations, their exact authority and noncacheable credential-safe projections.</summary>
internal sealed class ApplicationEditorContract : IOpenApiDocumentTransformer
{
    private const string Lifecycle = "ApplicationLifecycle";
    private const string Reason = "reason";
    private const string Confirm = "confirm";
    private const string Maintenance = "maintenance";
    private const string Security = "security";
    private const string Configuration = "configuration";
    private const string Collection = "/api/v1/applications";
    private const string Details = Collection + "/{id}";
    private const string Replacement = Details + "/secret-replacements";
    private const string Disable = Details + ApplicationLifecycleRules.DisableSuffix;
    private const string Enable = Details + ApplicationLifecycleRules.EnableSuffix;
    private const string ScopePath = Details + ApplicationScopeRules.Suffix;
    private const string ReadScopesOperation = "GetApplicationScopes";
    private const string AssignScopesOperation = "AssignApplicationScopes";
    private const string ScopePrecondition = "One canonical strong application ETag; missing is 428, malformed is 400 and stale is 412.";
    private const string LifecyclePermission = "applications.disable";
    private const string DisableOperation = "DisableApplication";
    private const string EnableOperation = "EnableApplication";
    private const string EditOperation = "EditApplication";
    private const string ReplacementOperation = "ReplaceApplicationSecret";
    private const string Credentials = "applications.credentials";
    private const string FreshMfa = "x-persisted-mfa-max-age-exclusive-seconds";
    private const int MfaSeconds = 300;
    private const string IfMatch = "If-Match";
    private const string PreconditionDescription = "One canonical strong resource-bound ETag from the latest read or mutation; missing is 428, malformed or stale is 412.";
    private const int VersionMinimum = 68;
    private const int VersionMaximum = 86;
    private const string CreateOperation = "CreateApplication";
    private const string DetailsOperation = "GetApplication";
    private const string Scheme = "AdminBearer";
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

    /// <summary>Applies metadata only to the named actual operations; list, token and dormant production inventories retain their own contracts.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        Apply(document, Collection, HttpMethod.Post, CreateOperation);
        Apply(document, Details, HttpMethod.Get, DetailsOperation);
        Apply(document, Details, HttpMethod.Put, EditOperation);
        Apply(document, Replacement, HttpMethod.Post, ReplacementOperation);
        Apply(document, Disable, HttpMethod.Post, DisableOperation);
        Apply(document, Enable, HttpMethod.Post, EnableOperation);
        Apply(document, ScopePath, HttpMethod.Get, ReadScopesOperation);
        Apply(document, ScopePath, HttpMethod.Put, AssignScopesOperation);
        LifecycleInput(document);
        return Task.CompletedTask;
    }

    /// <summary>Publishes the exact finite wire vocabulary and true-only confirmation shared by the actual lifecycle routes.</summary>
    private static void LifecycleInput(OpenApiDocument document)
    {
        if (document.Components?.Schemas?.TryGetValue(Lifecycle, out var value) == true && value is OpenApiSchema schema)
        {
            schema.Properties![Reason] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MaxLength = ApplicationLifecycleRules.MaximumReasonLength,
                Enum = [JsonValue.Create(Maintenance), JsonValue.Create(Security), JsonValue.Create(Configuration)]
            };
            schema.Properties[Confirm] = new OpenApiSchema { Type = JsonSchemaType.Boolean, Enum = [JsonValue.Create(true)] };
            schema.Required = new HashSet<string> { Reason, Confirm };
        }
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
        operation.Extensions[Permission] = new JsonNodeExtension(JsonValue.Create(path is Disable or Enable ? LifecyclePermission : path == Replacement ? Credentials : read ? Read : Write));
        if (path is Replacement or Disable or Enable || path == ScopePath && !read)
        {
            operation.Extensions[FreshMfa] = new JsonNodeExtension(JsonValue.Create(MfaSeconds));
        }
        Parameters(operation, create, read, path == ScopePath);
        Responses(operation, create, read, path == ScopePath);
    }

    /// <summary>Documents exact resource identity, mandatory update precondition and each native browser mutation proof.</summary>
    private static void Parameters(OpenApiOperation operation, bool create, bool read, bool assignment)
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
                Description = assignment ? ScopePrecondition : PreconditionDescription,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = VersionMinimum, MaxLength = VersionMaximum }
            });
        }
    }

    /// <summary>Requires no-store for every outcome and a strong version for every committed mutation.</summary>
    private static void Responses(OpenApiOperation operation, bool create, bool read, bool assignment)
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
                    Required = !read || assignment,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                };
            }
        }
    }
}
