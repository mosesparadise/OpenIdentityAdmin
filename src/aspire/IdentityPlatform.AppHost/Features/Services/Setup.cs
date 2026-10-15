using IdentityPlatform.Database.Hosting;

namespace IdentityPlatform.AppHost.Features.Services;

internal static class Setup
{
    internal static ServiceResources AddServices(this IDistributedApplicationBuilder builder, DatabaseResources database)
    {
        var migrator = builder.AddProject<Projects.IdentityPlatform_Migrator>("migrator")
            .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
            .WithEnvironment("Database__Provider", database.Provider)
            .WithEnvironment("LocalDevelopment__ProvisionRuntimeRoles", "true")
            .WithEnvironment("LocalDevelopment__PlatformPassword", database.PlatformPassword)
            .WithEnvironment("LocalDevelopment__SessionsPassword", database.SessionsPassword)
            .WithReference(database.Platform).WithReference(database.Sessions)
            .WaitFor(database.Platform).WaitFor(database.Sessions);

        var auth = builder.AddProject<Projects.IdentityPlatform_AuthServer>("auth")
            .WithHttpsEndpoint(name: "https");
        var api = builder.AddProject<Projects.IdentityPlatform_Admin_Api>("admin-api")
            .WithHttpsEndpoint(name: "https");
        var bff = builder.AddProject<Projects.IdentityPlatform_Admin_Bff>("admin-bff")
            .WithHttpsEndpoint(name: "https");
        var worker = builder.AddProject<Projects.IdentityPlatform_Worker>("worker")
            .WithHttpsEndpoint(name: "https");

        foreach (var service in new[] { auth, api, worker })
        {
            service.WithEnvironment("Database__Provider", database.Provider)
                .WithEnvironment("ConnectionStrings__platform", database.PlatformRuntime)
                .WaitForCompletion(migrator)
                .WithHttpHealthCheck("/health", endpointName: "https");
        }

        bff.WithEnvironment("Database__Provider", database.Provider)
            .WithEnvironment("ConnectionStrings__adminsessions", database.SessionsRuntime)
            .WaitForCompletion(migrator)
            .WithHttpHealthCheck("/health", endpointName: "https");

        return new ServiceResources(auth, migrator);
    }
}
