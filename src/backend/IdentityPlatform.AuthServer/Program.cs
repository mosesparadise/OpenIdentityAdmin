using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformServiceDefaults();
try
{
    var provider = DatabaseProviderParser.Parse(builder.Configuration["Database:Provider"]);
    builder.Services.AddIdentityPersistence(provider,
        builder.Configuration["ConnectionStrings:platform"] ?? "");
    builder.Services.AddHealthChecks().Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
        "storage", services => new SchemaReadinessCheck<PlatformDbContext>(services,
            SchemaCatalog.Platform(provider)), failureStatus: null, tags: ["ready"], timeout: TimeSpan.FromSeconds(5)));
}
catch (ArgumentException)
{
    Console.Error.WriteLine("Invalid database configuration. Set Database:Provider and the required connection string.");
    return 2;
}

var app = builder.Build();
app.MapPlatformHealthEndpoints();
await app.RunAsync();
return 0;
