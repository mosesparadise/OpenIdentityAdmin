using System.Globalization;
using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.Testing;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Hosts real account-only AuthServer processes against an isolated migrated runtime-role database.</summary>
internal sealed class AdministratorEnvironment : IAsyncDisposable
{
    private const string InitializationFailure = "Protected administrator initialization failed.";
    private const string CleanupFailure = "Administrator fixture cleanup failed.";
    private const string DevelopmentArgument = "--environment=Development";
    private const string DisabledPilotArgument = "--Pilot:Enabled=false";
    private const string DisabledAccountsArgument = "--Identity:AdministratorAccounts:Enabled=false";
    private const string ConnectionParameter = "administrator-runtime";
    private const string IssuerVariable = "Identity__Issuer";
    private const string IssuerSetting = "Identity:Issuer";
    private const string EnvironmentVariable = "DOTNET_ENVIRONMENT";
    private const string Development = "Development";
    private const string ProviderVariable = "Database__Provider";
    private const string ConnectionVariable = "ConnectionStrings__platform";
    private const string PilotVariable = "Pilot__Enabled";
    private const string Disabled = "false";
    private const string Enabled = "true";
    private const string AccountsVariable = "Identity__AdministratorAccounts__Enabled";
    private const string HealthPath = "/health";
    private const string ProtectionPrefix = "Identity:Protection:";
    private const string SettingSeparator = ":";
    private const string EnvironmentSeparator = "__";
    private const string PasswordSuffix = ":Password";
    private const string ParameterSeparator = "-";
    private const int HttpTimeoutSeconds = 20;

    private const string Primary = "administrator-auth";
    private const string Secondary = "administrator-secondary";
    private const string Https = "https";
    private const int StartupSeconds = 90;
    private IDistributedApplicationTestingBuilder? builder;
    private DistributedApplication? application;
    private ProcessGeneration[] processes = [];
    private const int NodeCount = 2;
    private const string ProcessId = "executable.pid";
    private const string MissingProcess = "Expected two distinct live administrator process generations.";
    private const int CleanupSeconds = 10;
    internal BootstrapDatabase Database
    {
        get; private set;
    } = null!;
    internal IssuerMaterialFixture Material { get; } = new();
    internal Uri Issuer
    {
        get; private set;
    } = null!;
    internal Uri OtherNode
    {
        get; private set;
    } = null!;

    /// <summary>Initializes through the actual protected Migrator process, then starts two hosts sharing durable issuer state.</summary>
    internal static async Task<AdministratorEnvironment> CreateAsync(BootstrapFixture fixture, string provider)
    {
        var environment = new AdministratorEnvironment();
        try
        {
            environment.Database = await fixture.LeaseAsync(provider);
            await using (var bootstrap = new BootstrapProcess(provider, environment.Database.RuntimeConnection))
            {
                await bootstrap.SendAsync(environment.Database.Input());
                var result = await bootstrap.CompleteAsync();
                Assert.True(result.ExitCode == 0, InitializationFailure);
            }
            await environment.StartAsync(provider);
            return environment;
        }
        catch
        {
            await environment.DisposeAsync();
            throw;
        }
    }

    /// <summary>Creates one graph-owned set of protection secrets and uses the primary endpoint as the canonical issuer.</summary>
    private async Task StartAsync(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(StartupSeconds));
        builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            [DevelopmentArgument, DisabledPilotArgument, DisabledAccountsArgument], deadline.Token);
        builder.Resources.Clear();
        var connection = builder.AddParameter(ConnectionParameter, () => Database.RuntimeConnection, secret: true);
        var primary = AddNode(Primary, provider, connection);
        var secondary = AddNode(Secondary, provider, connection);
        foreach (var resource in new[] { primary, secondary })
        {
            resource.WithEnvironment(IssuerVariable, primary.GetEndpoint(Https));
        }
        AddProtection(primary, secondary);
        application = await builder.BuildAsync(deadline.Token);
        await application.StartAsync(deadline.Token);
        await application.ResourceNotifications.WaitForResourceHealthyAsync(Primary, deadline.Token);
        await application.ResourceNotifications.WaitForResourceHealthyAsync(Secondary, deadline.Token);
        processes = CaptureProcesses();
        Issuer = application.GetEndpoint(Primary, Https);
        OtherNode = application.GetEndpoint(Secondary, Https);
        Material.Settings[IssuerSetting] = Issuer.AbsoluteUri;
    }

    /// <summary>Registers only the real account host; no pilot bootstrap, client, token flow or management grant is enabled.</summary>
    private IResourceBuilder<ProjectResource> AddNode(string name, string provider, IResourceBuilder<ParameterResource> connection) =>
        builder!.AddProject<Projects.IdentityPlatform_AuthServer>(name)
            .WithHttpsEndpoint(name: Https)
            .WithEnvironment(EnvironmentVariable, Development)
            .WithEnvironment(ProviderVariable, provider)
            .WithEnvironment(ConnectionVariable, connection)
            .WithEnvironment(PilotVariable, Disabled)
            .WithEnvironment(AccountsVariable, Enabled)
            .WithHttpHealthCheck(HealthPath, endpointName: Https);

    /// <summary>Shares synthetic protected material through secret parameters without placing credentials on process command lines.</summary>
    private void AddProtection(params IResourceBuilder<ProjectResource>[] nodes)
    {
        foreach (var setting in Material.Settings.Where(setting => setting.Key.StartsWith(ProtectionPrefix, StringComparison.Ordinal)))
        {
            var variable = setting.Key.Replace(SettingSeparator, EnvironmentSeparator, StringComparison.Ordinal);
            var secret = setting.Key.EndsWith(PasswordSuffix, StringComparison.Ordinal)
                ? builder!.AddParameter(setting.Key.Replace(SettingSeparator, ParameterSeparator, StringComparison.Ordinal).ToLowerInvariant(), () => setting.Value!, secret: true) : null;
            foreach (var node in nodes)
            {
                if (secret is null)
                {
                    node.WithEnvironment(variable, setting.Value!);
                }
                else
                {
                    node.WithEnvironment(variable, secret);
                }
            }
        }
    }

    /// <summary>Uses a real validated TLS connection while allowing a test to select a node without changing HTTP authority.</summary>
    internal HttpClient Browser(CookieContainer cookies, bool secondary = false) => new(new AdministratorTransport(Issuer,
        secondary ? OtherNode.Port : Issuer.Port, cookies))
    {
        BaseAddress = Issuer,
        Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds)
    };

    /// <summary>Records distinct native process generations and their descendants after both nodes become healthy.</summary>
    private ProcessGeneration[] CaptureProcesses()
    {
        var roots = new List<int>();
        foreach (var name in new[] { Primary, Secondary })
        {
            Assert.True(application!.ResourceNotifications.TryGetCurrentState(name, out var state), MissingProcess);
            var value = state!.Snapshot.Properties.Single(property => property.Name == ProcessId).Value;
            roots.Add(Convert.ToInt32(value, CultureInfo.InvariantCulture));
        }
        Assert.True(roots.All(id => id > 0) && roots.Distinct().Count() == NodeCount, MissingProcess);
        return roots.SelectMany(ProcessTreeEvidence.Capture).Distinct().ToArray();
    }

    /// <summary>Stops real processes before deleting their database and protected material, attempting every owned cleanup.</summary>
    public async ValueTask DisposeAsync()
    {
        var failed = false;
        try
        {
            if (application is not null)
            {
                await application.DisposeAsync();
            }
        }
        catch (Exception) { failed = true; }
        try
        {
            if (builder is not null)
            {
                await builder.DisposeAsync();
            }
        }
        catch (Exception) { failed = true; }
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
            await ProcessTreeEvidence.WaitForTerminationAsync(processes, deadline.Token);
        }
        catch (Exception) { failed = true; }
        try
        {
            if (Database is not null)
            {
                await Database.DisposeAsync();
            }
        }
        catch (Exception) { failed = true; }
        try
        {
            Material.Dispose();
        }
        catch (Exception) { failed = true; }
        Assert.False(failed, CleanupFailure);
    }
}
