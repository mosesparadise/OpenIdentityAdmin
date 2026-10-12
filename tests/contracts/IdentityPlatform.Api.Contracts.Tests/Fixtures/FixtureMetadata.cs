using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace IdentityPlatform.Api.Contracts.Tests.Fixtures;

/// <summary>Adds synthetic security and concurrency metadata while proving hostile description handling in real renderers.</summary>
internal static class FixtureMetadata
{
    private const string HostileDescription = "NON-SHIPPED tooling fixture. <img alt=\"\" src=\"data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7\" onload=window.fixtureInjected=true>";
    private const string BrowserScheme = "BrowserSession";
    private const string ApiScheme = "AdminBearer";
    private const string Bearer = "bearer";
    private const string JwtFormat = "JWT";
    private const string PermissionExtension = "x-required-permission";
    private const string VersionExtension = "x-downstream-api-version";
    private const string WritePermission = "examples.write";
    private const string ReadPermission = "examples.read";
    private const string ItemSuffix = "/{id}";
    private const string SuccessResponse = "200";
    private const string VersionHeader = "ETag";
    private const string VersionDescription = "Version required for edits";

    /// <summary>Install final fixture metadata after production transformation without changing authoritative operation schemas.</summary>
    internal static void Apply(OpenApiDocument document, bool browserSurface)
    {
        document.Info.Description = HostileDescription;
        var name = browserSurface ? BrowserScheme : ApiScheme;
        document.Components ??= new();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [name] = browserSurface
                ? new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = ContractValues.CookieName }
                : new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = Bearer, BearerFormat = JwtFormat }
        };
        foreach (var path in document.Paths)
        {
            foreach (var pair in path.Value.Operations!)
            {
                ApplyOperation(document, pair.Value, pair.Key, path.Key, name, browserSurface);
            }
        }
    }

    /// <summary>Keep each sample operation's required permission, proof/version headers and supported response metadata explicit.</summary>
    private static void ApplyOperation(OpenApiDocument document, OpenApiOperation operation, HttpMethod method, string path, string scheme, bool browserSurface)
    {
        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(scheme, document)] = [] }];
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions[PermissionExtension] = new JsonNodeExtension(JsonValue.Create(method == HttpMethod.Put ? WritePermission : ReadPermission));
        if (browserSurface)
        {
            operation.Extensions[VersionExtension] = new JsonNodeExtension(JsonValue.Create(1));
        }
        if (method == HttpMethod.Put)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(Header(ContractValues.IfMatchHeader));
            if (browserSurface)
            {
                operation.Parameters.Add(Header(ContractValues.ProofHeader));
            }
        }
        if (method == HttpMethod.Get && path.EndsWith(ItemSuffix, StringComparison.Ordinal))
        {
            ((OpenApiResponse)operation.Responses![SuccessResponse]).Headers = new Dictionary<string, IOpenApiHeader>
            {
                [VersionHeader] = new OpenApiHeader { Description = VersionDescription, Schema = new OpenApiSchema { Type = JsonSchemaType.String } }
            };
        }
    }

    /// <summary>Create an independently specified required string header for the synthetic consumer contract.</summary>
    private static OpenApiParameter Header(string name) => new()
    {
        Name = name,
        In = ParameterLocation.Header,
        Required = true,
        Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1 }
    };
}
