using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.Governance.Driver.Features;
using IdentityPlatform.Governance.Driver.Features.Protocol;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Governance.Driver.Composition;

/// <summary>Reuses the native fixture composition without starting AuthServer, loading ambient configuration or listening on HTTP.</summary>
internal static class DriverApplicationExtensions
{
    /// <summary>Builds configuration exclusively from previously bounded, allowlisted synthetic issuer inputs.</summary>
    internal static IConfigurationRoot AddConfigurations(this DriverInput input) =>
        new ConfigurationBuilder().AddInMemoryCollection(input.IssuerSettings).Build();

    /// <summary>Creates real persistence and protection services, preserving explicit certificate ownership on every failure.</summary>
    internal static DriverServices ConfigureServices(this DriverInput input, Action<DriverStage> observe)
    {
        var services = new ServiceCollection().AddLogging(logging => logging.ClearProviders())
            .AddIdentityPersistence(DatabaseProviderParser.Parse(input.Provider), input.RuntimeConnection);
        var configuration = input.AddConfigurations();
        using var configurationOwner = configuration as IDisposable;
        observe(DriverStage.Credentials);
        var credentials = services.AddIssuerProtection(configuration) ?? throw new InvalidOperationException();
        ServiceProvider? provider = null;
        try
        {
            services.AddAdministratorFactors(credentials.Issuer).AddAdministratorCeremonies()
                .AddAdministratorGovernance().AddFeatures();
            observe(DriverStage.ServiceValidation);
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            observe(DriverStage.Protection);
            provider.VerifyIssuerProtection();
            return new DriverServices(provider, credentials);
        }
        catch
        {
            try
            {
                provider?.Dispose();
            }
            finally { credentials.Dispose(); }
            throw;
        }
    }
}

/// <summary>Retains imported certificates until every scoped consumer and service provider has been released.</summary>
internal sealed class DriverServices(ServiceProvider provider, IssuerCredentials credentials) : IAsyncDisposable
{
    internal ServiceProvider Provider => provider;

    /// <summary>Always releases certificate owners even if native service disposal fails.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await provider.DisposeAsync();
        }
        finally { credentials.Dispose(); }
    }
}
