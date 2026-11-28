using System.Text.Json.Nodes;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Describes implemented application editor operations, their exact authority and noncacheable credential-safe projections.</summary>
internal sealed class ApplicationEditorContract : IOpenApiDocumentTransformer
{
    private const string Clone = Details + ApplicationCloneRules.Suffix;
    private const string CloneOperation = "CloneBrowserApplication";
    private const string CloneSchema = "ApplicationClone";
    private const string CloneConfirmation = "confirmed";
    private const string Lifecycle = "ApplicationLifecycle";
    private const string Reason = "reason";
    private const string Confirm = "confirm";
    private const string Maintenance = "maintenance";
    private const string Security = "security";
    private const string Configuration = "configuration";
    private const string Collection = "/bff/api/v1/applications";
    private const string Details = Collection + "/{id}";
    private const string Replacement = Details + "/secret-replacements";
    private const string Disable = Details + ApplicationLifecycleRules.DisableSuffix;
    private const string Enable = Details + ApplicationLifecycleRules.EnableSuffix;
    private const string ScopePath = Details + ApplicationScopeRules.Suffix;
    private const string ReadScopesOperation = "GetBrowserApplicationScopes";
    private const string AssignScopesOperation = "AssignBrowserApplicationScopes";
    private const string ScopePrecondition = "One canonical strong application ETag; missing is 428, malformed is 400 and stale is 412.";
    private const string LifecyclePermission = "applications.disable";
    private const string DisableOperation = "DisableBrowserApplication";
    private const string EnableOperation = "EnableBrowserApplication";
    private const string EditOperation = "EditBrowserApplication";
    private const string ReplacementOperation = "ReplaceBrowserApplicationSecret";
    private const string Credentials = "applications.credentials";
    private const string FreshMfa = "x-persisted-mfa-max-age-exclusive-seconds";
    private const int MfaSeconds = 300;
    private const string IfMatch = "If-Match";
    private const string PreconditionDescription = "One canonical strong resource-bound ETag from the latest read or mutation; missing is 428, malformed or stale is 412.";
    private const int VersionMinimum = 68;
    private const int VersionMaximum = 86;
    internal const string KeyMachineOperation = "RegisterBrowserKeyedMachineApplication";
    internal const string KeyMachineSummary = "Register a machine application with a public RSA authentication key";
    internal const string KeyReplacementOperation = "ReplaceBrowserApplicationPublicKey";
    internal const string KeyReplacementSummary = "Replace the registered public key using the current application ETag";
    private const string Permissions = "x-required-permissions";
    internal const string MachineOperation = "RegisterBrowserMachineApplication";
    internal const string MachineSummary = "Register a confidential machine client for assigned API scopes";
    internal const string BrowserOperation = "RegisterPublicBrowserApplication";
    internal const string BrowserSummary = "Register a public browser client with separate HTTPS origins, exact callbacks and S256";
    internal const string NativeOperation = "CreateBrowserNativeApplication";
    internal const string NativeSummary = "Register a public native application with exact claimed HTTPS callbacks and S256";
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
        Apply(document, Collection + ApplicationEditorRules.NativeRegistrationSuffix, HttpMethod.Post, NativeOperation);
        Apply(document, Collection + ApplicationEditorRules.BrowserRegistrationSuffix, HttpMethod.Post, BrowserOperation);
        Apply(document, Collection + MachineClientRules.Suffix, HttpMethod.Post, MachineOperation);
        Apply(document, Collection + MachineClientRules.KeyRegistrationSuffix, HttpMethod.Post, KeyMachineOperation);
        Apply(document, Details + MachineClientRules.KeyReplacementSuffix, HttpMethod.Post, KeyReplacementOperation);
        Apply(document, Clone, HttpMethod.Post, CloneOperation);
        Apply(document, Details, HttpMethod.Get, DetailsOperation);
        Apply(document, Details, HttpMethod.Put, EditOperation);
        Apply(document, Replacement, HttpMethod.Post, ReplacementOperation);
        Apply(document, Disable, HttpMethod.Post, DisableOperation);
        Apply(document, Enable, HttpMethod.Post, EnableOperation);
        Apply(document, ScopePath, HttpMethod.Get, ReadScopesOperation);
        Apply(document, ScopePath, HttpMethod.Put, AssignScopesOperation);
        IdentityPlatform.Management.Features.Applications.ApplicationRetirementContract.Apply(document, browser: true);
        LifecycleInput(document);
        CloneInput(document);
        return Task.CompletedTask;
    }

    /// <summary>Documents explicit confirmation without accepting native source configuration or credentials as clone input.</summary>
    private static void CloneInput(OpenApiDocument document)
    {
        if (document.Components?.Schemas?.TryGetValue(CloneSchema, out var value) == true && value is OpenApiSchema schema)
        {
            schema.Properties![CloneConfirmation] = new OpenApiSchema { Type = JsonSchemaType.Boolean, Enum = [JsonValue.Create(true)] };
            schema.Required ??= new HashSet<string>();
            schema.Required.Add(CloneConfirmation);
        }
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
        var registerKey = name == KeyMachineOperation;
        var create = registerKey || path == Collection || path == Collection + ApplicationEditorRules.NativeRegistrationSuffix || path == Collection + ApplicationEditorRules.BrowserRegistrationSuffix || path == Collection + MachineClientRules.Suffix;
        var read = method == HttpMethod.Get;
        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(Scheme, document)] = [] }];
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions[Permission] = new JsonNodeExtension(JsonValue.Create(path is Disable or Enable ? LifecyclePermission : (path == Replacement || name == KeyReplacementOperation) ? Credentials : read ? Read : Write));
        if (registerKey)
        {
            operation.Extensions[Permissions] = new JsonNodeExtension(new JsonArray(Write, Credentials));
        }
        if (registerKey || name == KeyReplacementOperation || path is Replacement or Disable or Enable or Clone || path == ScopePath && !read)
        {
            operation.Extensions[FreshMfa] = new JsonNodeExtension(JsonValue.Create(MfaSeconds));
        }
        operation.Extensions[Downstream] = new JsonNodeExtension(JsonValue.Create(1));
        Parameters(operation, create, read, path == ScopePath);
        Responses(operation, create || path == Clone, read, path == ScopePath);
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
