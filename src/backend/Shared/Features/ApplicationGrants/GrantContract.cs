using System.Text.Json.Nodes;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.OpenApi;

namespace IdentityPlatform.Management.Features.ApplicationGrants;

/// <summary>Shares the finite grant wire contract between independently compiled API and browser hosts.</summary>
internal static class GrantContract
{
    private const string Api = "/api/v1/applications/{id}/grant-revocations";
    private const string Browser = "/bff";
    private const string Job = "/{jobId}";
    private const string BearerScheme = "AdminBearer";
    private const string BrowserScheme = "BrowserSession";
    private const string Permission = "x-required-permission";
    private const string Permissions = "x-required-permissions";
    private const string FreshMfa = "x-persisted-mfa-max-age-exclusive-seconds";
    private const string Downstream = "x-downstream-api-version";
    private const int FreshSeconds = 300;
    private const string IfMatch = "If-Match";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string Id = "id";
    private const string JobId = "jobId";
    private const string Cache = "Cache-Control";
    private const string NoStore = "no-store";
    private const string Version = "ETag";
    private const string Location = "Location";
    private const string Accepted = "202";
    private const string Ok = "200";
    private const string GuidPattern = "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";
    private const int GuidLength = 36;
    private const int MinimumVersion = 68;
    private const int CsrfMaximum = 4096;

    /// <summary>Changes only the four implemented operations, leaving dormant production contracts empty.</summary>
    internal static void Apply(OpenApiDocument document, bool browser)
    {
        var collection = (browser ? Browser : string.Empty) + Api;
        Operation(document, collection, HttpMethod.Post, browser, single: true);
        Operation(document, collection, HttpMethod.Get, browser, single: false);
        Operation(document, collection + Job, HttpMethod.Get, browser, single: true);
        Operation(document, collection + Job + GrantRevocationRules.ContinueSuffix, HttpMethod.Post, browser, single: true);
        GrantSchemas.Apply(document);
    }

    /// <summary>Documents current independent permissions and original finite preconditions without supplying runtime authority.</summary>
    private static void Operation(OpenApiDocument document, string path, HttpMethod method, bool browser, bool single)
    {
        if (!document.Paths.TryGetValue(path, out var item) || item.Operations?.TryGetValue(method, out var operation) != true || operation is null)
        {
            return;
        }
        var write = method == HttpMethod.Post;
        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(browser ? BrowserScheme : BearerScheme, document)] = [] }];
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions[Permission] = new JsonNodeExtension(JsonValue.Create(GrantRevocationRules.Permission));
        if (write)
        {
            operation.Extensions[FreshMfa] = new JsonNodeExtension(JsonValue.Create(FreshSeconds));
        }
        else
        {
            operation.Extensions[Permissions] = new JsonNodeExtension(new JsonArray(GrantRevocationRules.ReadPermission, GrantRevocationRules.Permission));
        }
        if (browser)
        {
            operation.Extensions[Downstream] = new JsonNodeExtension(JsonValue.Create(1));
        }
        Parameters(operation, path.Contains(Job, StringComparison.Ordinal), write, browser);
        Responses(operation, write, single);
    }

    /// <summary>Requires exact path identities, one scoped key and original application version, plus native browser CSRF on writes.</summary>
    private static void Parameters(OpenApiOperation operation, bool job, bool write, bool browser)
    {
        operation.Parameters = [Parameter(Id, ParameterLocation.Path, new() { Type = JsonSchemaType.String, MinLength = 1, MaxLength = ApplicationProjection.IdentifierLength })];
        if (job)
        {
            operation.Parameters.Add(Parameter(JobId, ParameterLocation.Path, Key()));
        }
        if (!write)
        {
            return;
        }
        operation.Parameters.Add(Parameter(IfMatch, ParameterLocation.Header, new()
        {
            Type = JsonSchemaType.String,
            MinLength = MinimumVersion,
            MaxLength = GrantRevocationRules.MaximumVersionLength
        }));
        operation.Parameters.Add(Parameter(GrantRevocationRules.KeyHeader, ParameterLocation.Header, Key()));
        if (browser)
        {
            operation.Parameters.Add(Parameter(Csrf, ParameterLocation.Header, new()
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = CsrfMaximum
            }));
        }
    }

    /// <summary>Builds a required parameter whose schema is shared with the actual finite parser.</summary>
    private static OpenApiParameter Parameter(string name, ParameterLocation location, OpenApiSchema schema) =>
        new()
        {
            Name = name,
            In = location,
            Required = true,
            Schema = schema
        };

    /// <summary>Excludes alternate UUID forms from the wire contract; runtime also rejects the empty UUID.</summary>
    internal static OpenApiSchema Key() => new() { Type = JsonSchemaType.String, MinLength = GuidLength, MaxLength = GuidLength, Pattern = GuidPattern };

    /// <summary>Documents no-store on all outcomes and exact job versions/status locations only on successful observations.</summary>
    private static void Responses(OpenApiOperation operation, bool write, bool single)
    {
        foreach (var (status, value) in operation.Responses!)
        {
            if (value is not OpenApiResponse response)
            {
                throw new InvalidOperationException();
            }
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[Cache] = new OpenApiHeader { Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String, Const = NoStore } };
            if (status != (write ? Accepted : Ok))
            {
                continue;
            }
            if (single)
            {
                response.Headers[Version] = new OpenApiHeader { Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            }
            if (write)
            {
                response.Headers[Location] = new OpenApiHeader { Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            }
        }
    }
}
