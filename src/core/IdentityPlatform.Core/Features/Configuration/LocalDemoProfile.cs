namespace IdentityPlatform.Core.Features.Configuration;

/// <summary>Defines the single explicitly enabled disposable development profile and its canonical local identities.</summary>
/// <example><code>var enabled = LocalDemoProfile.IsEnabled(configuredValue, environmentName); // Rejects activation outside Development.</code></example>
public static class LocalDemoProfile
{
    private const string TrueValue = "true";
    private const string FalseValue = "false";
    private const string Development = "Development";
    public const string Section = "LocalDemo";
    public const string EnabledSetting = "Enabled";
    public const string IssuerHost = "issuer.dev.localhost";
    public const string ClientHost = "bff.dev.localhost";
    public const string ApiHost = "api.dev.localhost";
    public const int IssuerPort = 17443;
    public const int ClientPort = 17444;
    public const int ApiPort = 17445;
    public const string Issuer = "https://issuer.dev.localhost:17443/";
    public const string Client = "https://bff.dev.localhost:17444/";
    public const string Api = "https://api.dev.localhost:17445/";
    public const string Callback = "https://bff.dev.localhost:17444/bff/callback";
    public const string AssertionKeyId = "local-demo-assertion";
    public const string MaterialRoot = "/run/identity-demo";
    public const string MaterialSection = "LocalDemoMaterial";
    public const string Failure = "Invalid local demo configuration. Use the explicit disposable Development profile.";

    /// <summary>Defaults to disabled and rejects malformed values or enabled profiles outside Development without echoing input.</summary>
    /// <example><code>LocalDemoProfile.IsEnabled("true", "Development"); // Explicit local activation only.</code></example>
    public static bool IsEnabled(string? value, string environmentName)
    {
        if (value is null || value.Equals(FalseValue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!value.Equals(TrueValue, StringComparison.OrdinalIgnoreCase) || !environmentName.Equals(Development, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalDemoConfigurationException();
        }
        return true;
    }
}

/// <summary>Reports a fixed diagnostic for local demo activation or material staging failures.</summary>
/// <example><code>throw new LocalDemoConfigurationException(); // Never includes supplied values.</code></example>
public sealed class LocalDemoConfigurationException() : Exception(LocalDemoProfile.Failure);
