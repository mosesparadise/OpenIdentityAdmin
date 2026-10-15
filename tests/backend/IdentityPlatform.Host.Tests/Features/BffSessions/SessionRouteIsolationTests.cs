using IdentityPlatform.Admin.Bff.Composition;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Host.Tests.Features.BffSessions;

/// <summary>Inspects real BFF production composition without adding a test-only introspection or authentication route.</summary>
public sealed class SessionRouteIsolationTests
{
    private static readonly string[] OperationalRoutes = ["/alive", "/health"];
    private static readonly Dictionary<string, string?> Settings = new()
    {
        ["Database:Provider"] = "PostgreSql",
        ["ConnectionStrings:adminsessions"] = "Host=localhost;Database=synthetic-route-inventory"
    };

    /// <summary>Requires the complete endpoint inventory and default service registrations to remain health-only.</summary>
    [Fact]
    public async Task Given_DefaultBffComposition_When_Started_Then_StoreDoesNotActivateAuthenticationOrManagement()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        const string BffSettings = "src/backend/IdentityPlatform.Admin.Bff/appsettings.json";
        builder.Configuration.AddJsonFile(Path.Combine(RepositoryRoot(), BffSettings)).AddInMemoryCollection(Settings);
        builder.WebHost.UseTestServer();
        builder.AddConfigurations().ConfigureServices();
        await using var app = builder.Build();
        app.ConfigureApplication();
        await app.StartAsync(TestContext.Current.CancellationToken);
        ProductionEndpointInventory.RequireOnly(app, OperationalRoutes);
        ProductionEndpointInventory.RequireNoDriverReference(typeof(SessionTicketStore).Assembly);
        Assert.Null(app.Services.GetService<SessionTicketStore>());
        Assert.Null(app.Services.GetService<SessionProtection>());
    }
    /// <summary>Resolves the checked-in BFF configuration independently of the test runner's working directory.</summary>
    private static string RepositoryRoot()
    {
        const string Solution = "IdentityPlatform.slnx";
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, Solution)))
            {
                return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException();
    }
}
