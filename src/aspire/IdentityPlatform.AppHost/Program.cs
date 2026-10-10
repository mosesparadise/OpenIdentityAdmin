using IdentityPlatform.AppHost.Features.Pilot;
using IdentityPlatform.Core.Features.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("This AppHost is for local Development only. Production provisioning is external.");
}

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

var migrator = builder.AddProject<Projects.IdentityPlatform_Migrator>("migrator")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithEnvironment("Database__Provider", provider)
    .WithEnvironment("LocalDevelopment__ProvisionRuntimeRoles", "true")
    .WithEnvironment("LocalDevelopment__PlatformPassword", platformPassword)
    .WithEnvironment("LocalDevelopment__SessionsPassword", sessionsPassword)
    .WithReference(platform).WithReference(sessions)
    .WaitFor(platform).WaitFor(sessions);

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
    service.WithEnvironment("Database__Provider", provider)
        .WithEnvironment("ConnectionStrings__platform", platformRuntime)
        .WaitForCompletion(migrator)
        .WithHttpHealthCheck("/health", endpointName: "https");
}

bff.WithEnvironment("Database__Provider", provider)
    .WithEnvironment("ConnectionStrings__adminsessions", sessionsRuntime)
    .WaitForCompletion(migrator)
    .WithHttpHealthCheck("/health", endpointName: "https");

builder.AddPilot(auth, migrator, platformRuntime, provider);

builder.Build().Run();
