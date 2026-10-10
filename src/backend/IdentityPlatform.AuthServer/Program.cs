using IdentityPlatform.AuthServer.Features.Accounts;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol;
using IdentityPlatform.AuthServer.Features.Protocol.Authorize;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.AuthServer.Features.Protocol.Token;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

var initializePilot = args.Contains("--initialize-pilot", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(value => value != "--initialize-pilot").ToArray());
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

IssuerCredentials? credentials;
try
{
    credentials = builder.Services.AddIssuerProtection(builder.Configuration);
}
catch (IssuerProtectionException)
{
    Console.Error.WriteLine(IssuerProtectionException.SafeMessage);
    return 2;
}

using (credentials)
{
    bool pilot;
    try
    {
        pilot = PilotConfiguration.IsEnabled(builder.Configuration, builder.Environment, credentials);
        if (initializePilot && !pilot)
        {
            throw new PilotConfigurationException();
        }
        if (pilot)
        {
            builder.Services.AddFirstPartyPilot(credentials!);
        }
    }
    catch (PilotConfigurationException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
    await using var app = builder.Build();
    try
    {
        if (credentials is not null)
        {
            app.Services.VerifyIssuerProtection();
        }
    }
    catch (IssuerProtectionException)
    {
        Console.Error.WriteLine(IssuerProtectionException.SafeMessage);
        return 2;
    }
    if (initializePilot)
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            PilotDatabaseBounds.Apply(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            await scope.ServiceProvider.GetRequiredService<PilotBootstrap>().InitializeAsync(deadline.Token);
            Console.WriteLine("First-party pilot initialization verified.");
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("First-party pilot initialization failed. Existing state was not repaired or re-enabled.");
            return 2;
        }
    }
    if (pilot)
    {
        app.UseRouting();
        app.UseRequestTimeouts();
        app.UseMiddleware<PilotRequestProtection>();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapMethods("/connect/authorize", ["GET", "POST"], AuthorizeEndpoint.HandleAsync);
        app.MapPost("/connect/token", TokenEndpoint.HandleAsync);
        app.MapRazorPages();
        app.MapAccountAssets();
    }
    app.MapPlatformHealthEndpoints();
    await app.RunAsync();
    return 0;
}
