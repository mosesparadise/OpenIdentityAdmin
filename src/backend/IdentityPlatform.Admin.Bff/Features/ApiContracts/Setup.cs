using Asp.Versioning;
using Microsoft.OpenApi;

namespace IdentityPlatform.Admin.Bff.Features.ApiContracts;

internal static class Setup
{
    internal const string DocumentName = "bff-v1";
    internal const string RoutePrefix = "/bff/api/v{version:apiVersion}";
    internal const int SupportedMajor = 1;
    private const string GroupFormat = "'bff-v'VVV";
    private const string InvalidMetadata = "ApiContracts:Metadata requires bounded Title and Description values.";

    /// <summary>Register explicit URL-segment versioning and validated native OpenAPI metadata without exposing documentation routes.</summary>
    internal static WebApplicationBuilder AddApiContracts(this WebApplicationBuilder builder)
    {
        builder.Services.AddApiContracts(builder.Configuration);
        return builder;
    }

    /// <summary>Shares validated apicontracts registration between explicit generic hosting and contract fixtures.</summary>
    internal static IServiceCollection AddApiContracts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenApiMetadataOptions>()
            .BindConfiguration(OpenApiMetadataOptions.SectionName, options => options.ErrorOnUnknownConfiguration = true)
            .Validate(options => options.IsValid(), InvalidMetadata)
            .ValidateOnStart();
        services.AddProblemDetails();
        services.AddApiVersioning(options =>
        {
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
            options.AssumeDefaultVersionWhenUnspecified = false;
            options.ReportApiVersions = true;
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = GroupFormat;
            options.SubstituteApiVersionInUrl = true;
        }).AddOpenApi(options =>
        {
            options.Document.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
            var includesVersion = options.Document.ShouldInclude;
            options.Document.ShouldInclude = description => includesVersion(description) && description.GroupName == options.Description.GroupName;
            options.Document.AddDocumentTransformer<MetadataTransformer>();
            options.Document.AddDocumentTransformer<BrowserSessions.BrowserSessionContractTransformer>();
        });
        return services;
    }

    /// <summary>Map only the supported management major, keeping identity protocol routes outside API versioning.</summary>
    internal static RouteGroupBuilder MapManagementGroup(this WebApplication app)
    {
        var versions = app.NewApiVersionSet().HasApiVersion(new ApiVersion(SupportedMajor)).ReportApiVersions().Build();
        return app.MapGroup(RoutePrefix).WithApiVersionSet(versions).MapToApiVersion(SupportedMajor);
    }
}
