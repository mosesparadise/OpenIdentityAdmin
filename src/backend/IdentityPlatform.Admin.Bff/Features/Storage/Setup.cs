using IdentityPlatform.Admin.Bff.Composition;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Admin.Bff.Features.Storage;

internal static class Setup
{
    private const string ProviderSetting = "Database:Provider";
    private const string SessionConnectionSetting = "ConnectionStrings:adminsessions";
    private const string StorageCheck = "storage";
    private const string ReadinessTag = "ready";
    private const int ReadinessSeconds = 5;

    /// <summary>Selects an explicitly registered provider and composes least-privilege storage with bounded readiness.</summary>
    internal static WebApplicationBuilder AddStorage(this WebApplicationBuilder builder)
    {
        var provider = DatabaseProviderRegistration.Providers.Select(builder.Configuration[ProviderSetting]);
        builder.Services.AddSessionPersistence(provider,
            builder.Configuration[SessionConnectionSetting] ?? "");
        builder.Services.AddHealthChecks().Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
            StorageCheck, services => new SchemaReadinessCheck<AdminSessionDbContext>(services,
                SchemaCatalog.Sessions(provider)), failureStatus: null, tags: [ReadinessTag], timeout: TimeSpan.FromSeconds(ReadinessSeconds)));
        return builder;
    }
}
