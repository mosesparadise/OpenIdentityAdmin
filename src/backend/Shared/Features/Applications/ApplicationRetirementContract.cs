using System.Text.Json.Nodes;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Management.Features.ApplicationGrants;
using Microsoft.OpenApi;

namespace IdentityPlatform.Management.Features.Applications;

/// <summary>Documents one terminal admission consistently across the independent API and browser surfaces.</summary>
internal static class ApplicationRetirementContract
{
    private const string VersionDescription = "Current linked grant job version; the application version is in the receipt.";
    private const string LocationDescription = "Existing status resource for the exact linked grant job.";

    private const string ApiPath = "/api/v1/applications/{id}/retirement";
    private const string BrowserPrefix = "/bff";
    private const string ApiOperation = "RetireApplication";
    private const string BrowserOperation = "RetireBrowserApplication";
    private const string ApiScheme = "AdminBearer";
    private const string BrowserScheme = "BrowserSession";
    private const string Permission = "x-required-permission";
    private const string Permissions = "x-required-permissions";
    private const string FreshMfa = "x-persisted-mfa-max-age-exclusive-seconds";
    private const string Downstream = "x-downstream-api-version";
    private const string IfMatch = "If-Match";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string Id = "id";
    private const string Cache = "Cache-Control";
    private const string NoStore = "no-store";
    private const string Version = "ETag";
    private const string Location = "Location";
    private const string Accepted = "202";
    private const int FreshSeconds = 300;
    private const int MinimumVersion = 68;
    private const int CsrfMaximum = 4096;

    /// <summary>Requires both permissions and fresh persisted MFA, retaining a separate receipt and job observation contract.</summary>
    internal static void Apply(OpenApiDocument document, bool browser)
    {
        var path = (browser ? BrowserPrefix : string.Empty) + ApiPath;
        if (!document.Paths.TryGetValue(path, out var item) || item.Operations?.TryGetValue(HttpMethod.Post, out var operation) != true ||
            operation is null || operation.OperationId != (browser ? BrowserOperation : ApiOperation))
        {
            return;
        }
        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(browser ? BrowserScheme : ApiScheme, document)] = [] }];
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions[Permission] = new JsonNodeExtension(JsonValue.Create(ApplicationRetirementRules.Permission));
        operation.Extensions[Permissions] = new JsonNodeExtension(new JsonArray(ApplicationRetirementRules.Permission, GrantRevocationRules.Permission));
        operation.Extensions[FreshMfa] = new JsonNodeExtension(JsonValue.Create(FreshSeconds));
        if (browser)
        {
            operation.Extensions[Downstream] = new JsonNodeExtension(JsonValue.Create(1));
        }
        Parameters(operation, browser);
        Responses(operation);
    }

    /// <summary>Publishes mandatory exact application precondition, canonical request key and native browser CSRF proof.</summary>
    private static void Parameters(OpenApiOperation operation, bool browser)
    {
        operation.Parameters =
        [
            Parameter(Id, ParameterLocation.Path, new() { Type = JsonSchemaType.String, MinLength = 1, MaxLength = ApplicationProjection.IdentifierLength }),
            Parameter(IfMatch, ParameterLocation.Header, new() { Type = JsonSchemaType.String, MinLength = MinimumVersion, MaxLength = GrantRevocationRules.MaximumVersionLength }),
            Parameter(GrantRevocationRules.KeyHeader, ParameterLocation.Header, GrantContract.Key())
        ];
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

    /// <summary>Constructs a required bounded parameter without accepting alternate destinations or identity forms.</summary>
    private static OpenApiParameter Parameter(string name, ParameterLocation location, OpenApiSchema schema) => new()
    {
        Name = name,
        In = location,
        Required = true,
        Schema = schema
    };

    /// <summary>Distinguishes the job ETag and status location from the terminal application version in the immutable receipt.</summary>
    private static void Responses(OpenApiOperation operation)
    {
        foreach (var (status, value) in operation.Responses!)
        {
            if (value is not OpenApiResponse response)
            {
                throw new InvalidOperationException();
            }
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[Cache] = new OpenApiHeader { Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String, Const = NoStore } };
            if (status == Accepted)
            {
                response.Headers[Version] = new OpenApiHeader { Required = true, Description = VersionDescription, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
                response.Headers[Location] = new OpenApiHeader { Required = true, Description = LocationDescription, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            }
        }
    }
}
