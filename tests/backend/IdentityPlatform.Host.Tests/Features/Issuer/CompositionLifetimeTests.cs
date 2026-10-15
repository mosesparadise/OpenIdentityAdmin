using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.AuthServer.Composition;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Pilot;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Host.Tests.Features.Issuer;

public sealed class CompositionLifetimeTests
{
    [Theory]
    [InlineData("normal")]
    [InlineData("configuration")]
    [InlineData("build")]
    [InlineData("protection")]
    [InlineData("bootstrap")]
    public async Task Actual_composition_keeps_keys_alive_through_host_disposal_and_releases_them_on_every_exit(string scenario)
    {
        using var fixture = new IssuerMaterialFixture();
        IssuerCredentials? material = null;
        DisposalProbe? probe = null;
        var verifiedBeforeContextDisposal = false;
        if (scenario == "protection")
        {
            File.WriteAllText(Path.Combine(fixture.Ring, "invalid.xml"), "<key>invalid</key>");
        }
        var failure = await Record.ExceptionAsync(async () =>
        {
            using var startup = new StartupContext(scenario == "bootstrap" ? ["--initialize-pilot"] : []);
            try
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    Args = startup.HostArguments,
                    ApplicationName = typeof(IssuerCredentials).Assembly.GetName().Name,
                    EnvironmentName = scenario == "bootstrap" ? "Development" : "Production"
                });
                builder.Configuration.AddInMemoryCollection(fixture.Settings);
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "PostgreSql",
                    ["ConnectionStrings:platform"] = "Host=localhost;Port=1;Database=fixture;Username=fixture;Password=synthetic",
                    ["Pilot:Enabled"] = scenario is "configuration" or "bootstrap" ? "true" : "false",
                    ["Pilot:UserName"] = null,
                    ["Pilot:Password"] = null,
                    ["Pilot:ClientSecret"] = null,
                    ["Pilot:RedirectUri"] = null
                });
                builder.AddConfigurations().ConfigureServices(startup);
                builder.Services.AddSingleton(_ => probe = new DisposalProbe(startup.Credentials!));
                if (scenario == "build")
                {
                    builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = true);
                    builder.Services.AddSingleton<BrokenService>();
                }
                await using var app = builder.Build();
                app.Services.GetRequiredService<DisposalProbe>();
                app.ConfigureApplication(startup);
                if (scenario == "bootstrap")
                {
                    Assert.Equal(2, await app.RunApplicationAsync(startup));
                }
            }
            finally
            {
                material = startup.Credentials;
                if (material is not null)
                {
                    verifiedBeforeContextDisposal = VerifySigningKey(material);
                }
            }
        });
        if (scenario == "configuration")
        {
            Assert.IsType<PilotConfigurationException>(failure);
        }
        else if (scenario == "build")
        {
            Assert.IsType<AggregateException>(failure);
        }
        else if (scenario == "protection")
        {
            Assert.IsType<IssuerProtectionException>(failure);
        }
        else
        {
            Assert.True(failure is null, "Composition produced an unexpected failure.");
        }
        Assert.NotNull(material);
        Assert.True(verifiedBeforeContextDisposal, "Credentials must remain usable until their startup context is disposed, including early failures.");
        if (scenario is not ("configuration" or "build"))
        {
            Assert.NotNull(probe);
            Assert.True(probe.Verified, "DI-owned objects must be able to use credentials while the application is disposing.");
        }
        Assert.Throws<CryptographicException>(() => material.Signing.GetRSAPrivateKey());
    }

    private static bool VerifySigningKey(IssuerCredentials credentials)
    {
        using var signer = credentials.Signing.GetRSAPrivateKey()!;
        using var verifier = credentials.Signing.GetRSAPublicKey()!;
        var data = new byte[] { 1, 2, 3 };
        var signature = signer.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return verifier.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private sealed class DisposalProbe(IssuerCredentials credentials) : IDisposable
    {
        internal bool Verified
        {
            get; private set;
        }

        public void Dispose()
        {
            Verified = VerifySigningKey(credentials);
        }
    }

    private interface IMissingDependency;

    private sealed class BrokenService
    {
        public BrokenService(IMissingDependency dependency) => ArgumentNullException.ThrowIfNull(dependency);
    }
}
