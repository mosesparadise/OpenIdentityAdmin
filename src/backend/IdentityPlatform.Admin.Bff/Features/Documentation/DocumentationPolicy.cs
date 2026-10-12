using System.Globalization;
using System.Text;
using Scalar.AspNetCore;

namespace IdentityPlatform.Admin.Bff.Features.Documentation;

/// <summary>Preserves the code-owned same-origin renderer policy and fixed document selector inventory.</summary>
internal static class DocumentationPolicy
{
    internal const string DocumentationRoot = "/docs";
    internal const string OpenApiRoot = "/openapi";
    internal const string ScalarRoute = "/docs/scalar";
    internal const string SwaggerRoute = "docs/swagger";
    internal const string ScalarAsset = "/docs/assets/scalar-bootstrap.js";
    internal const string SwaggerAsset = "/docs/assets/swagger.js";
    internal const string ThemeAsset = "/docs/assets/theme.css";
    internal const string ScriptMediaType = "text/javascript";
    internal const string StyleMediaType = "text/css";
    internal const string ScalarHead = "<script src=\"/docs/assets/scalar-bootstrap.js\"></script>";
    private const string SameOriginScript = "'self'";
    private const string NonceScript = "'self' 'nonce-{0}'";
    private const string ContentPolicy = "default-src 'none'; script-src {0}; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
    private const string NoStore = "no-store";
    private const string NoSniff = "nosniff";
    private const string ReferrerHeader = "Referrer-Policy";
    private const string NoReferrer = "no-referrer";
    private static readonly CompositeFormat NonceFormat = CompositeFormat.Parse(NonceScript);
    private static readonly CompositeFormat PolicyFormat = CompositeFormat.Parse(ContentPolicy);
    private static readonly string[] ScalarSelectors = ["/v1", "/bff-v1", "/scalar.js", "/scalar.aspnetcore.js", "/favicon.svg"];

    /// <summary>Apply response hardening and reject unsupported Scalar document/asset selectors before renderer execution.</summary>
    internal static async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments(DocumentationRoot) || context.Request.Path.StartsWithSegments(OpenApiRoot))
        {
            context.Response.OnStarting(() => ApplyHeadersAsync(context));
        }
        if (context.Request.Path.StartsWithSegments(ScalarRoute, out var remaining) &&
            remaining.Value is { Length: > 1 } value && !ScalarSelectors.Contains(value, StringComparer.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next(context);
    }

    /// <summary>Read the actual Scalar nonce at response start and retain all existing no-store and browser isolation headers.</summary>
    private static Task ApplyHeadersAsync(HttpContext context)
    {
        var nonce = context.Items[ScalarOptions.NonceHttpContextItemKey] as string;
        var script = nonce is null ? SameOriginScript : string.Format(CultureInfo.InvariantCulture, NonceFormat, nonce);
        context.Response.Headers.ContentSecurityPolicy = string.Format(CultureInfo.InvariantCulture, PolicyFormat, script);
        context.Response.Headers.CacheControl = NoStore;
        context.Response.Headers.XContentTypeOptions = NoSniff;
        context.Response.Headers[ReferrerHeader] = NoReferrer;
        return Task.CompletedTask;
    }
}
