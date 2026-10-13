using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ApiComposition = IdentityPlatform.Admin.Api.Composition.WebApplicationExtensions;
using BffComposition = IdentityPlatform.Admin.Bff.Composition.WebApplicationExtensions;

namespace IdentityPlatform.Api.Contracts.Tests.Fixtures;

/// <summary>Starts actual API/BFF production composition without synthetic contract endpoints or documentation activation.</summary>
internal sealed class ProductionContractHost(WebApplication app, HttpClient client) : IAsyncDisposable
{
    private const string ProviderSetting = "Database:Provider";
    private const string PlatformSetting = "ConnectionStrings:platform";
    private const string SessionsSetting = "ConnectionStrings:adminsessions";
    private const string TelemetrySetting = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string ApiProject = "src/backend/IdentityPlatform.Admin.Api";
    private const string BffProject = "src/backend/IdentityPlatform.Admin.Bff";
    private const string Provider = "PostgreSql";
    private const string UnusedConnection = "Host=localhost;Port=1;Database=synthetic;Username=synthetic;Password=synthetic";
    internal WebApplication App => app;
    internal HttpClient Client => client;

    /// <summary>Uses the exact production extension chain with only in-process transport and syntactically valid unused storage settings.</summary>
    internal static async Task<ProductionContractHost> StartAsync(bool browserSurface)
    {
        var builder = CreateBuilder(browserSurface);
        if (browserSurface)
        {
            BffComposition.ConfigureServices(BffComposition.AddConfigurations(builder));
        }
        else
        {
            ApiComposition.ConfigureServices(ApiComposition.AddConfigurations(builder));
        }
        var application = builder.Build();
        try
        {
            if (browserSurface)
            {
                BffComposition.ConfigureApplication(application);
            }
            else
            {
                ApiComposition.ConfigureApplication(application);
            }
            await application.StartAsync(TestContext.Current.CancellationToken);
            return new ProductionContractHost(application, application.GetTestClient());
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    /// <summary>Loads each real project's settings and existing repository-path helper; no fixture contract generator or endpoint mapper runs.</summary>
    private static WebApplicationBuilder CreateBuilder(bool browserSurface)
    {
        var assembly = browserSurface ? typeof(BffComposition).Assembly : typeof(ApiComposition).Assembly;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = assembly.GetName().Name,
            ContentRootPath = Path.Combine(ContractFixtureConfiguration.RepositoryRoot(), browserSurface ? BffProject : ApiProject),
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ProviderSetting] = Provider,
            [PlatformSetting] = UnusedConnection,
            [SessionsSetting] = UnusedConnection,
            [TelemetrySetting] = string.Empty
        });
        return builder;
    }

    /// <summary>Disposes transport before native host services, including startup failures handled by the factory.</summary>
    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
    }
}
