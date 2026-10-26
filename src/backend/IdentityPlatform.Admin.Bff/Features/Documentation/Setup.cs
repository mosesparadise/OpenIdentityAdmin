using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

namespace IdentityPlatform.Admin.Bff.Features.Documentation;

/// <summary>Registers viewer options separately from dormant renderer routes; this feature does not supply authorization.</summary>
internal static class Setup
{
    private const string InvalidOptions = "Documentation:Viewer requires bounded PageTitle, BrowserDocumentLabel and AdminDocumentLabel values.";
    private const string BrowserDocument = "bff-v1";
    private const string AdminDocument = "v1";

    /// <summary>Validate presentation settings without mapping documentation or adding an activation switch.</summary>
    internal static WebApplicationBuilder AddDocumentation(this WebApplicationBuilder builder)
    {
        builder.Services.AddDocumentation(builder.Configuration);
        return builder;
    }

    /// <summary>Shares validated documentation registration between explicit generic hosting and contract fixtures.</summary>
    internal static IServiceCollection AddDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DocumentationOptions>()
            .BindConfiguration(DocumentationOptions.SectionName, options => options.ErrorOnUnknownConfiguration = true)
            .Validate(options => options.IsValid(), InvalidOptions).ValidateOnStart();
        return services;
    }

    /// <summary>Map pinned read-only renderers for controlled fixtures; the production pipeline deliberately never calls this method.</summary>
    internal static void MapDocumentation(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<IOptions<DocumentationOptions>>().Value;
        app.Use(DocumentationPolicy.InvokeAsync);
        app.MapOpenApi().WithDocumentPerVersion();
        MapScalar(app, settings);
        MapSwagger(app, settings);
        MapAssets(app);
    }

    /// <summary>Keep local assets, nonce, no credentials and disabled interaction while applying safely rendered text.</summary>
    private static void MapScalar(WebApplication app, DocumentationOptions settings) => app.MapScalarApiReference(DocumentationPolicy.ScalarRoute, options =>
    {
        options.PersistentAuthentication = false;
        options.WithTitle(DocumentationAssets.ScalarTitle(settings.PageTitle))
            .AddDocument(BrowserDocument, settings.BrowserDocumentLabel, isDefault: true)
            .AddDocument(AdminDocument, settings.AdminDocumentLabel)
            .HideTestRequestButton().HideClientButton().DisableDefaultFonts().DisableAgent().DisableTelemetry().HideDeveloperTools()
            .WithNonce().AddHeadContent(DocumentationPolicy.ScalarHead).WithCustomCss(DocumentationAssets.ReadTheme());
    });

    /// <summary>Serve a fresh encoded template for each request and preserve disabled credential persistence and submit methods.</summary>
    private static void MapSwagger(WebApplication app, DocumentationOptions settings) => app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = DocumentationPolicy.SwaggerRoute;
        options.IndexStream = () => DocumentationAssets.RenderSwagger(settings);
        options.SupportedSubmitMethods([]);
        options.ConfigObject.PersistAuthorization = false;
    });

    /// <summary>Expose only the fixed embedded scripts and stylesheet, excluding assets from the API contract.</summary>
    private static void MapAssets(WebApplication app)
    {
        app.MapGet(DocumentationPolicy.ScalarAsset, () => Results.Content(DocumentationAssets.Read(DocumentationAssets.ScalarBootstrap), DocumentationPolicy.ScriptMediaType)).ExcludeFromDescription();
        app.MapGet(DocumentationPolicy.SwaggerAsset, () => Results.Content(DocumentationAssets.Read(DocumentationAssets.SwaggerBootstrap), DocumentationPolicy.ScriptMediaType)).ExcludeFromDescription();
        app.MapGet(DocumentationPolicy.ThemeAsset, () => Results.Content(DocumentationAssets.ReadTheme(), DocumentationPolicy.StyleMediaType)).ExcludeFromDescription();
    }
}
