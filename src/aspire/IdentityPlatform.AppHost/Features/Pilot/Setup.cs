using IdentityPlatform.AppHost.Features.Services;

namespace IdentityPlatform.AppHost.Features.Pilot;

internal static class Setup
{
    private const string EnablementKey = "Pilot:Enabled";
    private const string Disabled = "false";
    private const string Enabled = "true";
    private const string InvalidEnablement = "Invalid pilot enablement setting.";
    private const string BootstrapResource = "pilot-bootstrap";
    private const string BootstrapArgument = "--initialize-pilot";
    private const string EnvironmentKey = "DOTNET_ENVIRONMENT";
    private const string Development = "Development";
    private const string ProviderKey = "Database__Provider";
    private const string PlatformConnectionKey = "ConnectionStrings__platform";
    private const string EnablementVariable = "Pilot__Enabled";
    private const string ConfigurationPrefix = "Pilot:";
    private const string PasswordKey = "Password";
    private const string ClientSecretKey = "ClientSecret";
    private const string VariablePrefix = "Pilot__";
    private const string ParameterPrefix = "pilot-";

    private static readonly string[] PilotKeys = ["UserName", "RedirectUri", PasswordKey, ClientSecretKey];

    /// <summary>Composes the explicit Development pilot and its one-shot bootstrap without activating administrator accounts.</summary>
    internal static void AddPilot(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> auth,
        IResourceBuilder<ProjectResource> migrator, ReferenceExpression platformRuntime, string provider, IssuerProtection protection)
    {
        var enabled = builder.Configuration[EnablementKey];
        if (enabled is null || string.Equals(enabled, Disabled, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (!string.Equals(enabled, Enabled, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(InvalidEnablement);
        }
        var bootstrap = builder.AddProject<Projects.IdentityPlatform_AuthServer>(BootstrapResource)
            .WithArgs(BootstrapArgument)
            .WithEnvironment(EnvironmentKey, Development)
            .WithEnvironment(ProviderKey, provider)
            .WithEnvironment(PlatformConnectionKey, platformRuntime)
            .WithEnvironment(EnablementVariable, Enabled)
            .WaitForCompletion(migrator);
        auth.WithEnvironment(EnvironmentKey, Development)
            .WithEnvironment(EnablementVariable, Enabled)
            .WaitForCompletion(bootstrap);
        protection.Apply(auth);
        protection.Apply(bootstrap);
        foreach (var key in PilotKeys)
        {
            var value = builder.Configuration[ConfigurationPrefix + key];
            if (value is null)
            {
                continue; // The enabled host rejects incomplete configuration; tests may inject before startup.
            }
            if (key is PasswordKey or ClientSecretKey)
            {
                bootstrap.WithEnvironment(VariablePrefix + key, builder.AddParameter(ParameterPrefix + key.ToLowerInvariant(), () => value, secret: true));
            }
            else
            {
                bootstrap.WithEnvironment(VariablePrefix + key, value);
            }
        }
    }
}
