using Scalar.AspNetCore;

namespace IdentityPlatform.Admin.Bff.Features.Documentation;

// Deliberately not called by the production pipeline. This is renderer composition, not authorization.
internal static class Setup
{
    internal static void MapDocumentation(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/docs") || context.Request.Path.StartsWithSegments("/openapi"))
            {
                context.Response.OnStarting(() =>
                {
                    var nonce = context.Items[ScalarOptions.NonceHttpContextItemKey] as string;
                    var script = nonce is null ? "'self'" : $"'self' 'nonce-{nonce}'";
                    context.Response.Headers.ContentSecurityPolicy = $"default-src 'none'; script-src {script}; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.Headers.XContentTypeOptions = "nosniff";
                    context.Response.Headers["Referrer-Policy"] = "no-referrer";
                    return Task.CompletedTask;
                });
            }
            // Scalar accepts a document-name route selector. Limit it to the supported inventory.
            if (context.Request.Path.StartsWithSegments("/docs/scalar", out var remaining) &&
                remaining.Value is { Length: > 1 } value &&
                value is not "/v1" and not "/bff-v1" and not "/scalar.js" and not "/scalar.aspnetcore.js" and not "/favicon.svg")
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            await next(context);
        });
        app.MapOpenApi().WithDocumentPerVersion();
        app.MapScalarApiReference("/docs/scalar", options =>
        {
            options.PersistentAuthentication = false;
            options
            .AddDocument("bff-v1", "Browser BFF v1", isDefault: true)
            .AddDocument("v1", "Downstream Admin API v1")
            .HideTestRequestButton().HideClientButton().DisableDefaultFonts().DisableAgent().DisableTelemetry().HideDeveloperTools()
            .WithNonce()
            .WithCustomCss(ReadText("tokens.css") + ReadText("theme.css"));
        });
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "docs/swagger";
            options.IndexStream = () => OpenAsset("index.html");
            options.SupportedSubmitMethods([]);
            options.ConfigObject.PersistAuthorization = false;
        });
        app.MapGet("/docs/assets/swagger.js", () => Results.Content(ReadText("swagger.js"), "text/javascript")).ExcludeFromDescription();
        app.MapGet("/docs/assets/theme.css", () => Results.Content(ReadText("tokens.css") + ReadText("theme.css"), "text/css")).ExcludeFromDescription();
    }

    private static Stream OpenAsset(string name) => typeof(Setup).Assembly.GetManifestResourceStream(
        "IdentityPlatform.Admin.Bff.Features.Documentation.Assets." + name)!;

    private static string ReadText(string name)
    {
        using var reader = new StreamReader(OpenAsset(name));
        return reader.ReadToEnd();
    }
}
