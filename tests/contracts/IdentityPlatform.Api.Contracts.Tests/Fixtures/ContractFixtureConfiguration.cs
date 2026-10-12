using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ApiContracts = IdentityPlatform.Admin.Api.Features.ApiContracts.Setup;
using BffContracts = IdentityPlatform.Admin.Bff.Features.ApiContracts.Setup;
using BffDocumentation = IdentityPlatform.Admin.Bff.Features.Documentation.Setup;

namespace IdentityPlatform.Api.Contracts.Tests.Fixtures;

/// <summary>Loads the selected real host's production settings and installs final synthetic contract metadata.</summary>
internal static class ContractFixtureConfiguration
{
    private const string RepositoryMarker = "global.json";
    private const string MissingRoot = "Repository root not found.";
    private const string ApiProject = "src/backend/IdentityPlatform.Admin.Api";
    private const string BffProject = "src/backend/IdentityPlatform.Admin.Bff";
    private const string HttpsAddress = "https://127.0.0.1:0";

    /// <summary>Create a deterministic native builder whose settings do not depend on the test process working directory.</summary>
    internal static WebApplicationBuilder CreateBuilder(bool browserSurface, bool realHttps,
        IReadOnlyDictionary<string, string?>? configuration = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.Combine(RepositoryRoot(), browserSurface ? BffProject : ApiProject),
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        if (configuration is not null)
        {
            builder.Configuration.AddInMemoryCollection(configuration);
        }
        if (realHttps)
        {
            builder.WebHost.UseUrls(HttpsAddress);
        }
        else
        {
            builder.WebHost.UseTestServer();
        }
        RegisterContracts(builder, browserSurface);
        return builder;
    }

    /// <summary>Resolve the checkout from build output rather than trusting the current working directory.</summary>
    internal static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, RepositoryMarker)))
        {
            root = root.Parent ?? throw new InvalidOperationException(MissingRoot);
        }
        return root.FullName;
    }

    /// <summary>Append fixture metadata after the versioning package's post-configuration so hostile test input reaches the document.</summary>
    private static void RegisterContracts(WebApplicationBuilder builder, bool browserSurface)
    {
        if (browserSurface)
        {
            BffContracts.AddApiContracts(builder);
            BffDocumentation.AddDocumentation(builder);
        }
        else
        {
            ApiContracts.AddApiContracts(builder);
        }
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<ExampleState>()));
        builder.Services.PostConfigure<OpenApiOptions>(browserSurface ? BffContracts.DocumentName : ApiContracts.DocumentName,
            options => options.AddDocumentTransformer((document, _, _) =>
            {
                FixtureMetadata.Apply(document, browserSurface);
                return Task.CompletedTask;
            }));
    }
}
