using System.Text.Json;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Database.Hosting;

namespace IdentityPlatform.AppHost.Features.LocalDemo;

/// <summary>Creates the explicit local container graph without changing provider registration or ordinary AppHost startup.</summary>
internal static class Setup
{
    private const string RunIdKey = "RunId";
    private const string RunIdSetting = "LocalDemo:RunId";
    private const string OwnedContainerPrefix = "oid-demo-";
    private const string NameSeparator = "-";
    private const string LabelArgument = "--label=";
    private const string LabelAssignment = "=";
    private const int RunIdLength = 32;
    private const string GuidFormat = "N";
    private const string MigratorResource = "migrator";
    private const string IssuerResource = "auth";
    private const string IssuerStage = "issuer";
    private const string ClientResource = "admin-bff";
    private const string ClientStage = "bff";
    private const string InitializerResource = "initializer";
    private const string ProviderVariable = "Database__Provider";
    private const string ProvisionRoles = "LocalDevelopment__ProvisionRuntimeRoles";
    private const string TrueValue = "true";
    private const string FalseValue = "false";
    private const string PlatformPassword = "LocalDevelopment__PlatformPassword";
    private const string SessionsPassword = "LocalDevelopment__SessionsPassword";
    private const string PlatformConnection = "ConnectionStrings__platform";
    private const string SessionsConnection = "ConnectionStrings__adminsessions";
    private const string IssuerVariable = "Identity__Issuer";
    private const string AccountsVariable = "Identity__AdministratorAccounts__Enabled";
    private const string ProtocolVariable = "Identity__AdministratorProtocol__Enabled";
    private const string RedirectVariable = "Identity__AdministratorProtocol__RedirectUri";
    private const string PublicKeyVariable = "Identity__AdministratorProtocol__PublicKey";
    private const string IssuerSigning = "IssuerSigning";
    private const string IssuerEncryption = "IssuerEncryption";
    private const string IssuerWrapping = "IssuerWrapping";
    private const string ClientStateSigning = "ClientStateSigning";
    private const string ClientStateEncryption = "ClientStateEncryption";
    private const string ClientWrapping = "ClientWrapping";
    private const string ClientAssertion = "ClientAssertion";
    private const string OperatorReference = "local-demo";
    private const string RegistrationVariable = "LOCAL_DEMO_REGISTRATION";
    private const string UpgradeVariable = "LOCAL_DEMO_UPGRADE";
    /// <summary>Checks activation before database or certificate generation and forbids publishing the disposable profile.</summary>
    internal static bool IsLocalDemo(this IDistributedApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(LocalDemoProfile.Section);
        if (section.Value is not null || section.GetChildren().Any(child =>
            (!child.Key.Equals(LocalDemoProfile.EnabledSetting, StringComparison.OrdinalIgnoreCase) && !child.Key.Equals(RunIdKey, StringComparison.OrdinalIgnoreCase)) || child.GetChildren().Any()))
        {
            throw new LocalDemoConfigurationException();
        }
        var enabled = LocalDemoProfile.IsEnabled(section[LocalDemoProfile.EnabledSetting], builder.Environment.EnvironmentName);
        if (enabled && builder.ExecutionContext.IsPublishMode)
        {
            throw new LocalDemoConfigurationException();
        }
        return enabled;
    }

    /// <summary>Separates schema provisioning, public initialization and runtime connections while retaining existing migrations.</summary>
    internal static IDistributedApplicationBuilder AddLocalDemo(this IDistributedApplicationBuilder builder, DatabaseResources database)
    {
        var run = builder.Configuration[RunIdSetting] ?? Guid.NewGuid().ToString(GuidFormat);
        if (run.Length != RunIdLength || !run.All(value => char.IsAsciiDigit(value) || value is >= 'a' and <= 'f'))
        {
            throw new LocalDemoConfigurationException();
        }
        foreach (var container in builder.Resources.OfType<ContainerResource>().ToArray())
        {
            builder.CreateResourceBuilder(container).WithContainerName(OwnedContainerPrefix + run + NameSeparator + container.Name)
                .WithContainerRuntimeArgs(LabelArgument + DemoContainers.RunLabel + LabelAssignment + run);
        }
        var migrator = builder.AddDemoContainer(MigratorResource, MigratorResource, run)
            .WithEnvironment(ProviderVariable, database.Provider)
            .WithEnvironment(ProvisionRoles, TrueValue)
            .WithEnvironment(PlatformPassword, database.PlatformPassword)
            .WithEnvironment(SessionsPassword, database.SessionsPassword)
            .WithReference(database.Platform).WithReference(database.Sessions).WaitFor(database.Platform).WaitFor(database.Sessions);
        var auth = builder.AddDemoContainer(IssuerResource, IssuerStage, run).WithDemoHttps(LocalDemoProfile.IssuerHost, LocalDemoProfile.IssuerPort)
            .WithEnvironment(ProviderVariable, database.Provider).WithEnvironment(PlatformConnection, database.PlatformRuntime)
            .WithEnvironment(IssuerVariable, LocalDemoProfile.Issuer).WithEnvironment(AccountsVariable, TrueValue)
            .WithEnvironment(ProtocolVariable, TrueValue).WithEnvironment(RedirectVariable, LocalDemoProfile.Callback)
            .WaitForCompletion(migrator);
        var bff = builder.AddDemoContainer(ClientResource, ClientStage, run).WithDemoHttps(LocalDemoProfile.ClientHost, LocalDemoProfile.ClientPort)
            .WithEnvironment(ProviderVariable, database.Provider).WithEnvironment(SessionsConnection, database.SessionsRuntime)
            .WaitForCompletion(migrator);
        var assertion = AddMaterial(builder, auth, bff);
        auth.WithEnvironment(PublicKeyVariable, assertion.PublicKey);
        AddInitializer(builder, database, migrator, run, assertion.PublicKey);
        return builder;
    }

    /// <summary>Generates seven distinct roles and sends each only to its owning host.</summary>
    private static DemoCertificate AddMaterial(IDistributedApplicationBuilder builder, IResourceBuilder<ContainerResource> auth, IResourceBuilder<ContainerResource> bff)
    {
        foreach (var role in new[] { IssuerSigning, IssuerEncryption, IssuerWrapping })
        {
            DemoCertificate.Create(builder, role, role == IssuerSigning).Apply(auth, role);
        }
        foreach (var role in new[] { ClientStateSigning, ClientStateEncryption, ClientWrapping })
        {
            DemoCertificate.Create(builder, role, role == ClientStateSigning).Apply(bff, role);
        }
        var assertion = DemoCertificate.Create(builder, ClientAssertion, signing: true);
        assertion.Apply(bff, ClientAssertion);
        return assertion;
    }

    /// <summary>Provides a run-owned exec target with Platform runtime credentials and public-only registration documents.</summary>
    private static void AddInitializer(IDistributedApplicationBuilder builder, DatabaseResources database, IResourceBuilder<ContainerResource> migrator, string run, string publicKey)
    {
        using var document = JsonDocument.Parse(publicKey);
        var registration = JsonSerializer.Serialize(new
        {
            operationId = Guid.NewGuid(),
            operatorReference = OperatorReference,
            redirectUri = LocalDemoProfile.Callback,
            publicKey = document.RootElement
        });
        var upgrade = JsonSerializer.Serialize(new
        {
            operationId = Guid.NewGuid(),
            operatorReference = OperatorReference,
            redirectUri = LocalDemoProfile.Callback,
            publicKey = document.RootElement,
            expectedRevision = 1
        });
        builder.AddDemoContainer(InitializerResource, InitializerResource, run)
            .WithEnvironment(ProviderVariable, database.Provider).WithEnvironment(PlatformConnection, database.PlatformRuntime)
            .WithEnvironment(ProvisionRoles, FalseValue)
            .WithEnvironment(RegistrationVariable, registration).WithEnvironment(UpgradeVariable, upgrade)
            .WaitForCompletion(migrator);
    }
}
