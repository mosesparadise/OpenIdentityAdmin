using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Admin.Bff.Features.Storage;

internal static class Setup
{
    internal static WebApplicationBuilder AddStorage(this WebApplicationBuilder builder)
    {
        var provider = DatabaseProviderParser.Parse(builder.Configuration["Database:Provider"]);
        builder.Services.AddSessionPersistence(provider,
            builder.Configuration["ConnectionStrings:adminsessions"] ?? "");
        builder.Services.AddHealthChecks().Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
            "storage", services => new SchemaReadinessCheck<AdminSessionDbContext>(services,
                SchemaCatalog.Sessions(provider)), failureStatus: null, tags: ["ready"], timeout: TimeSpan.FromSeconds(5)));
        return builder;
    }
}
