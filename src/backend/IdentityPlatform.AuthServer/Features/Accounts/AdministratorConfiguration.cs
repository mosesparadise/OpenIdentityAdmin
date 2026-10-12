using IdentityPlatform.AuthServer.Features.Issuer.Credentials;

namespace IdentityPlatform.AuthServer.Features.Accounts;

/// <summary>Validates the explicit administrator feature switch without leaking configuration values.</summary>
internal static class AdministratorConfiguration
{
    internal const string Section = "Identity:AdministratorAccounts";
    private const string Enabled = "Enabled";
    private const string True = "true";
    private const string False = "false";

    /// <summary>Defaults to disabled; rejects unknown/nested fields, malformed booleans and activation without durable issuer protection.</summary>
    internal static bool IsEnabled(IConfiguration configuration, IssuerCredentials? credentials)
    {
        var section = configuration.GetSection(Section);
        if (section.Value is not null || section.GetChildren().Any(child =>
            !string.Equals(child.Key, Enabled, StringComparison.OrdinalIgnoreCase) || child.GetChildren().Any()))
        {
            throw new AdministratorConfigurationException();
        }
        var value = section[Enabled];
        if (value is null || string.Equals(value, False, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!string.Equals(value, True, StringComparison.OrdinalIgnoreCase) || credentials is null)
        {
            throw new AdministratorConfigurationException();
        }
        return true;
    }
}

/// <summary>Provides a fixed, non-sensitive startup diagnostic for invalid administrator activation.</summary>
internal sealed class AdministratorConfigurationException : Exception
{
    private const string SafeMessage = "Invalid administrator account configuration. Explicit activation requires configured issuer protection.";

    /// <summary>Creates an error that never embeds the supplied setting or protection paths.</summary>
    internal AdministratorConfigurationException() : base(SafeMessage) { }
}
