using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.AuthServer.Features.Storage;

internal static class Setup
{
    internal static WebApplicationBuilder AddStorage(this WebApplicationBuilder builder)
    {
        var provider = DatabaseProviderParser.Parse(builder.Configuration["Database:Provider"]);
        builder.Services.AddIdentityPersistence(provider,
            builder.Configuration["ConnectionStrings:platform"] ?? "");
        builder.Services.AddHealthChecks().Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
            "storage", services => new SchemaReadinessCheck<PlatformDbContext>(services,
                SchemaCatalog.Platform(provider)), failureStatus: null, tags: ["ready"], timeout: TimeSpan.FromSeconds(5)));
        return builder;
    }
}
