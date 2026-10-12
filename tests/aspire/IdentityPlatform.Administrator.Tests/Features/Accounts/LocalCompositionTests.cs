using System.Net;
using Aspire.Hosting.Testing;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.AppHost.Tests.Features;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Starts the actual unchanged AppHost graph to establish accounts-only wiring separately from the focused two-node fixture.</summary>
[Collection("administrator-bootstrap")]
public sealed class LocalCompositionTests
{
    private const string Development = "--environment=Development";
    private const string ProviderSetting = "Database:Provider";
    private const string AccountsSetting = "Identity:AdministratorAccounts:Enabled";
    private const string PilotSetting = "Pilot:Enabled";
    private const string Enabled = "true";
    private const string Disabled = "false";
    private const string Auth = "auth";
    private const string Https = "https";
    private const string Platform = "platform";
    private const string Migrations = "PlatformMigrations";
    private const string PilotBootstrap = "pilot-bootstrap";
    private const int StartupMinutes = 3;

    /// <summary>Independent account activation serves native forms without automatically provisioning an administrator or enabling the pilot.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AccountsOnlyConfiguration_When_RealAppHostStarts_Then_FormsAreAvailableAndBootstrapRemainsExplicit(string provider)
    {
        using var material = new IssuerMaterialFixture();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(StartupMinutes));
        material.Settings[ProviderSetting] = provider;
        material.Settings[AccountsSetting] = Enabled;
        material.Settings[PilotSetting] = Disabled;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>([Development], (_, host) =>
        {
            host.Configuration ??= new ConfigurationManager();
            host.Configuration.AddInMemoryCollection(material.Settings);
        }, deadline.Token);
        Assert.DoesNotContain(builder.Resources, resource => resource.Name == PilotBootstrap);
        LifecycleEvidence evidence;
        await using (var app = await builder.BuildAsync(deadline.Token))
        {
            await app.StartAsync(deadline.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(Auth, deadline.Token);
            using var browser = new HttpClient { BaseAddress = app.GetEndpoint(Auth, Https) };
            using var form = await browser.GetAsync(AdministratorJourney.Enrollment, deadline.Token);
            Assert.Equal(HttpStatusCode.OK, form.StatusCode);
            using var status = await browser.GetAsync(AdministratorJourney.Security, deadline.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
            var options = new DbContextOptionsBuilder<PlatformDbContext>();
            PersistenceRegistration.ConfigureDatabase(options, DatabaseProviderParser.Parse(provider), (await app.GetConnectionStringAsync(Platform, deadline.Token))!, Migrations);
            await using var database = new PlatformDbContext(options.Options);
            Assert.Empty(await database.Users.ToArrayAsync(deadline.Token));
            evidence = LifecycleEvidence.Capture(app);
        }
        await evidence.AssertRemovedAsync();
    }
}
