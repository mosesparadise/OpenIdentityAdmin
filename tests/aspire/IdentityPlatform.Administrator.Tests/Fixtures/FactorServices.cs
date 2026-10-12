using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Reopens actual AuthServer factor registrations with the existing encrypted durable issuer fixture.</summary>
internal sealed class FactorServices : IAsyncDisposable
{
    private readonly IssuerCredentials credentials;
    internal ServiceProvider Services
    {
        get;
    }

    /// <summary>Registers runtime-only database credentials and validates the durable ring before native factor use.</summary>
    internal FactorServices(string provider, BootstrapDatabase database, IssuerMaterialFixture material, TimeProvider? clock = null)
    {
        var services = new ServiceCollection().AddLogging(logging => logging.ClearProviders())
            .AddIdentityPersistence(DatabaseProviderParser.Parse(provider), database.RuntimeConnection);
        credentials = services.AddIssuerProtection(material.Configuration)!;
        services.AddAdministratorFactors(credentials.Issuer).AddAdministratorCeremonies();
        services.AddSingleton(clock ?? TimeProvider.System);
        Services = services.BuildServiceProvider(validateScopes: true);
        try
        {
            Services.VerifyIssuerProtection();
        }
        catch
        {
            Services.Dispose();
            credentials.Dispose();
            throw;
        }
    }

    /// <summary>Disposes all native adapters before their externally owned issuer credentials.</summary>
    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        credentials.Dispose();
    }
}
