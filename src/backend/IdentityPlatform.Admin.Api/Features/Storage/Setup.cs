using IdentityPlatform.Admin.Api.Composition;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Admin.Api.Features.Storage;

internal static class Setup
{
    private const string ProviderSetting = "Database:Provider";
    private const string PlatformConnectionSetting = "ConnectionStrings:platform";
    private const string StorageCheck = "storage";
    private const string ReadinessTag = "ready";
    private const int ReadinessSeconds = 5;

    /// <summary>Selects an explicitly registered provider and composes least-privilege storage with bounded readiness.</summary>
    internal static WebApplicationBuilder AddStorage(this WebApplicationBuilder builder)
    {
        builder.Services.AddStorage(builder.Configuration);
        return builder;
    }

    /// <summary>Registers the same provider and bounded readiness in explicitly owned generic hosting.</summary>
    internal static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = DatabaseProviderRegistration.Providers.Select(configuration[ProviderSetting]);
        services.AddIdentityPersistence(provider, configuration[PlatformConnectionSetting] ?? "");
        services.AddHealthChecks().Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
            StorageCheck, services => new SchemaReadinessCheck<PlatformDbContext>(services,
                SchemaCatalog.Platform(provider)), failureStatus: null, tags: [ReadinessTag], timeout: TimeSpan.FromSeconds(ReadinessSeconds)));
        return services;
    }
}
