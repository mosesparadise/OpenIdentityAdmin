using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.AppHost.Features.Database;

internal static class Setup
{
    internal static DatabaseResources AddDatabase(this IDistributedApplicationBuilder builder)
    {
        var provider = DatabaseProviderParser.Parse(builder.Configuration["Database:Provider"] ?? "PostgreSql").ToString();

        var platformPassword = builder.AddParameter("platform-runtime-password", new GenerateParameterDefault
        {
            MinLength = 40,
            MinLower = 2,
            MinUpper = 2,
            MinNumeric = 2,
            Special = false
        }, secret: true, persist: false);
        var sessionsPassword = builder.AddParameter("sessions-runtime-password", new GenerateParameterDefault
        {
            MinLength = 40,
            MinLower = 2,
            MinUpper = 2,
            MinNumeric = 2,
            Special = false
        }, secret: true, persist: false);

        IResourceBuilder<IResourceWithConnectionString> platform;
        IResourceBuilder<IResourceWithConnectionString> sessions;
        ReferenceExpression platformRuntime;
        ReferenceExpression sessionsRuntime;
        if (provider == "PostgreSql")
        {
            var database = builder.AddPostgres("database")
                .WithImageTag("18")
                .WithImageSHA256("74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336");
            platform = database.AddDatabase("platform");
            sessions = database.AddDatabase("adminsessions");
            var endpoint = database.GetEndpoint("tcp");
            platformRuntime = ReferenceExpression.Create($"Host={endpoint.Property(EndpointProperty.Host)};Port={endpoint.Property(EndpointProperty.Port)};Database=platform;Username=platform_runtime;Password={platformPassword}");
            sessionsRuntime = ReferenceExpression.Create($"Host={endpoint.Property(EndpointProperty.Host)};Port={endpoint.Property(EndpointProperty.Port)};Database=adminsessions;Username=sessions_runtime;Password={sessionsPassword}");
        }
        else
        {
            var database = builder.AddSqlServer("database")
                .WithImageTag("2022-latest")
                .WithImageSHA256("4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090");
            platform = database.AddDatabase("platform");
            sessions = database.AddDatabase("adminsessions");
            var endpoint = database.GetEndpoint("tcp");
            // The disposable local SQL container uses its self-signed transport certificate.
            // This does not alter HTTPS certificate/issuer validation for application traffic.
            platformRuntime = ReferenceExpression.Create($"Server={endpoint.Property(EndpointProperty.Host)},{endpoint.Property(EndpointProperty.Port)};Database=platform;User ID=platform_runtime;Password={platformPassword};Encrypt=True;TrustServerCertificate=True");
            sessionsRuntime = ReferenceExpression.Create($"Server={endpoint.Property(EndpointProperty.Host)},{endpoint.Property(EndpointProperty.Port)};Database=adminsessions;User ID=sessions_runtime;Password={sessionsPassword};Encrypt=True;TrustServerCertificate=True");
        }

        return new DatabaseResources(provider, platformPassword, sessionsPassword, platform, sessions, platformRuntime, sessionsRuntime);
    }
}
