using System.Text.Json.Nodes;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using Microsoft.OpenApi;

namespace IdentityPlatform.Management.Features.Catalogue;

/// <summary>Shares catalogue authority and precondition documentation between independently authenticated API and browser hosts.</summary>
internal static class CatalogueContract
{
    private const string Detail = "/{id}";
    private const string Job = "/{jobId}";
    private const string Bearer = "AdminBearer";
    private const string Browser = "BrowserSession";
    private const string Permission = "x-required-permission";
    private const string FreshMfa = "x-persisted-mfa-max-age-exclusive-seconds";
    private const string Downstream = "x-downstream-api-version";
    private const string ScopeRead = "scopes.read";
    private const string ScopeWrite = "scopes.write";
    private const string AudienceRead = "audiences.read";
    private const string AudienceWrite = "audiences.write";
    private const string Id = "id";
    private const string JobId = "jobId";
    private const string IfMatch = "If-Match";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string Cache = "Cache-Control";
    private const string NoStore = "no-store";
    private const string ETag = "ETag";
    private const string Location = "Location";
    private const string Ok = "200";
    private const string Created = "201";
    private const string Accepted = "202";
    private const string MinimumPage = "1";
    private const int MfaSeconds = 300;
    private const int CsrfLength = 4096;

    /// <summary>Describes mapped finite operations only; absent dormant surfaces remain absent.</summary>
    internal static void Apply(OpenApiDocument document, bool browser)
    {
        foreach (var kind in new[] { CatalogueVersion.Scope, CatalogueVersion.Audience })
        {
            var path = (browser ? CataloguePaths.BrowserRoot : CataloguePaths.ApiRoot) + CataloguePaths.Collection(kind);
            ApplyOperation(document, path, HttpMethod.Get, kind, browser);
            ApplyOperation(document, path, HttpMethod.Post, kind, browser);
            foreach (var method in new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Delete })
            {
                ApplyOperation(document, path + Detail, method, kind, browser);
            }
            ApplyOperation(document, path + CataloguePaths.Deletions + Job, HttpMethod.Get, kind, browser);
            if (browser)
            {
                ApplyOperation(document, path + CataloguePaths.Antiforgery, HttpMethod.Get, kind, browser);
            }
        }
        CatalogueSchemas.Apply(document);
    }

    /// <summary>Records current persisted permission, fresh-MFA writes and independent browser/native authentication without authorizing runtime work.</summary>
    private static void ApplyOperation(OpenApiDocument document, string path, HttpMethod method, string kind, bool browser)
    {
        if (!document.Paths.TryGetValue(path, out var item) || item.Operations?.TryGetValue(method, out var operation) != true || operation is null)
        {
            return;
        }
        var write = method != HttpMethod.Get;
        var token = path.EndsWith(CataloguePaths.Antiforgery, StringComparison.Ordinal);
        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(browser ? Browser : Bearer, document)] = [] }];
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        if (!token)
        {
            operation.Extensions[Permission] = new JsonNodeExtension(JsonValue.Create(kind == CatalogueVersion.Scope ? write ? ScopeWrite : ScopeRead : write ? AudienceWrite : AudienceRead));
        }
        if (write)
        {
            operation.Extensions[FreshMfa] = new JsonNodeExtension(JsonValue.Create(MfaSeconds));
        }
        if (browser && !token)
        {
            operation.Extensions[Downstream] = new JsonNodeExtension(JsonValue.Create(1));
        }
        Parameters(operation, path, method, browser, token);
        Responses(operation, path, method, browser, token);
    }

    /// <summary>Documents exact path, paging and mutation proof grammar independently of input object schemas.</summary>
    private static void Parameters(OpenApiOperation operation, string path, HttpMethod method, bool browser, bool token)
    {
        operation.Parameters = [];
        if (path.EndsWith(Detail, StringComparison.Ordinal))
        {
            operation.Parameters.Add(Parameter(Id, ParameterLocation.Path, new()
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = CatalogueRules.IdentifierLength
            }));
        }
        else if (path.EndsWith(Job, StringComparison.Ordinal))
        {
            operation.Parameters.Add(Parameter(JobId, ParameterLocation.Path, CatalogueSchemas.Key()));
        }
        else if (method == HttpMethod.Get && !token)
        {
            operation.Parameters.Add(Parameter(ApplicationListQuery.SearchParameter, ParameterLocation.Query, new()
            {
                Type = JsonSchemaType.String,
                MaxLength = ApplicationListQuery.MaximumSearch
            }, false));
            operation.Parameters.Add(Parameter(ApplicationListQuery.PageParameter, ParameterLocation.Query, new()
            {
                Type = JsonSchemaType.Integer,
                Minimum = MinimumPage,
                Maximum = ApplicationListQuery.MaximumPage.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }, false));
            operation.Parameters.Add(Parameter(ApplicationListQuery.SizeParameter, ParameterLocation.Query, new()
            {
                Type = JsonSchemaType.Integer,
                Minimum = MinimumPage,
                Maximum = ApplicationListQuery.MaximumSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }, false));
        }
        if (method == HttpMethod.Put || method == HttpMethod.Delete)
        {
            operation.Parameters.Add(Parameter(IfMatch, ParameterLocation.Header, CatalogueSchemas.Version()));
        }
        if (browser && method != HttpMethod.Get)
        {
            operation.Parameters.Add(Parameter(Csrf, ParameterLocation.Header, new()
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = CsrfLength
            }));
        }
    }

    /// <summary>Requires no-store everywhere and exact version/status-location headers only on the corresponding successful observations.</summary>
    private static void Responses(OpenApiOperation operation, string path, HttpMethod method, bool browser, bool token)
    {
        foreach (var (status, value) in operation.Responses!)
        {
            if (value is not OpenApiResponse response)
            {
                throw new InvalidOperationException();
            }
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[Cache] = new OpenApiHeader { Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String, Const = NoStore } };
            if (status is not (Ok or Created or Accepted) || token)
            {
                continue;
            }
            if (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Get && path.EndsWith(Detail, StringComparison.Ordinal))
            {
                response.Headers[ETag] = new OpenApiHeader { Required = true, Schema = CatalogueSchemas.Version() };
            }
            if (method == HttpMethod.Delete || method == HttpMethod.Post && !browser)
            {
                response.Headers[Location] = new OpenApiHeader { Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
            }
        }
    }

    /// <summary>Builds one explicitly bounded parameter; optional paging differs from required path/header authority preconditions.</summary>
    private static OpenApiParameter Parameter(string name, ParameterLocation location, OpenApiSchema schema, bool required = true) =>
        new()
        {
            Name = name,
            In = location,
            Required = required,
            Schema = schema
        };
}
