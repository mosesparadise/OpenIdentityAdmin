using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace IdentityPlatform.Api.Contracts.Tests.Fixtures;

internal static class FixtureMetadata
{
    internal static void Apply(OpenApiDocument document, bool browserSurface)
    {
        document.Info.Description = "NON-SHIPPED tooling fixture. <img src=x onerror=window.fixtureInjected=true>";
        var name = browserSurface ? "BrowserSession" : "AdminBearer";
        document.Components ??= new();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [name] = browserSurface
                ? new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = "__Host-contract-fixture" }
                : new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" }
        };
        foreach (var path in document.Paths)
        {
            foreach (var pair in path.Value.Operations!)
            {
                var operation = pair.Value;
                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(name, document)] = [] }];
                operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                operation.Extensions["x-required-permission"] = new JsonNodeExtension(JsonValue.Create(pair.Key == HttpMethod.Put ? "examples.write" : "examples.read"));
                if (browserSurface)
                {
                    operation.Extensions["x-downstream-api-version"] = new JsonNodeExtension(JsonValue.Create(1));
                }
                if (pair.Key == HttpMethod.Put)
                {
                    operation.Parameters ??= [];
                    operation.Parameters.Add(Header("If-Match"));
                    if (browserSurface)
                    {
                        operation.Parameters.Add(Header("X-CSRF-TOKEN"));
                    }
                }
                if (pair.Key == HttpMethod.Get && path.Key.EndsWith("/{id}", StringComparison.Ordinal))
                {
                    ((OpenApiResponse)operation.Responses!["200"]).Headers = new Dictionary<string, IOpenApiHeader>
                    {
                        ["ETag"] = new OpenApiHeader { Description = "Version required for edits", Schema = new OpenApiSchema { Type = JsonSchemaType.String } }
                    };
                }
            }
        }
    }

    private static OpenApiParameter Header(string name) => new()
    {
        Name = name,
        In = ParameterLocation.Header,
        Required = true,
        Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1 }
    };
}
