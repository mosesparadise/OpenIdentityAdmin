using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Describes only the implemented native-token acquisition, without advertising unimplemented mutation routes or API permission.</summary>
internal sealed class ApplicationTokenContract : IOpenApiDocumentTransformer
{
    private const string Scheme = "BrowserSession";
    private const string Cookie = "__Host-identity-admin-session";
    private const string CacheHeader = "Cache-Control";
    private const string CacheDescription = "Native request tokens must not be cached.";
    private const string NoStore = "no-store";

    /// <summary>Requires the protected browser session and no-store metadata on every finite token-endpoint response.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (document.Paths.TryGetValue(ApplicationAntiforgeryEndpoint.Path, out var path) && path.Operations?.TryGetValue(HttpMethod.Get, out var operation) == true && operation is not null)
        {
            document.Components ??= new();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[Scheme] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = Cookie };
            operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(Scheme, document)] = [] }];
            foreach (var response in operation.Responses!.Values.OfType<OpenApiResponse>())
            {
                response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                response.Headers[CacheHeader] = new OpenApiHeader { Description = CacheDescription, Schema = new OpenApiSchema { Type = JsonSchemaType.String, Const = NoStore } };
            }
        }
        return Task.CompletedTask;
    }
}
