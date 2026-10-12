using Asp.Versioning;
using Microsoft.OpenApi;

namespace IdentityPlatform.Admin.Bff.Features.ApiContracts;

internal static class Setup
{
    internal const string DocumentName = "bff-v1";
    internal const string RoutePrefix = "/bff/api/v{version:apiVersion}";
    internal const int SupportedMajor = 1;

    internal static WebApplicationBuilder AddApiContracts(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddApiVersioning(options =>
        {
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
            options.AssumeDefaultVersionWhenUnspecified = false;
            options.ReportApiVersions = true;
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'bff-v'VVV";
            options.SubstituteApiVersionInUrl = true;
        }).AddOpenApi(options =>
        {
            options.Document.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
            var includesVersion = options.Document.ShouldInclude;
            options.Document.ShouldInclude = description => includesVersion(description) && description.GroupName == options.Description.GroupName;
            options.Document.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "IdentityPlatform Admin BFF browser API",
                    Version = "1",
                    Description = "Contract foundation. No production management operations are implemented."
                };
                document.Servers = [new OpenApiServer { Url = "/" }];
                return Task.CompletedTask;
            });
        });
        return builder;
    }

    internal static RouteGroupBuilder MapManagementGroup(this WebApplication app)
    {
        var versions = app.NewApiVersionSet().HasApiVersion(new ApiVersion(SupportedMajor)).ReportApiVersions().Build();
        return app.MapGroup(RoutePrefix).WithApiVersionSet(versions).MapToApiVersion(SupportedMajor);
    }
}
