using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Requires independent explicit protocol activation and the same immutable public profile used by initialization.</summary>
internal static class AdministratorProtocolConfiguration
{
    internal const string Section = "Identity:AdministratorProtocol";
    internal const string Enabled = "Enabled";
    internal const string Redirect = "RedirectUri";
    internal const string PublicKey = "PublicKey";
    private const string True = "true";
    private const string False = "false";
    private static readonly HashSet<string> Fields = new([Enabled, Redirect, PublicKey], StringComparer.OrdinalIgnoreCase);

    /// <summary>Defaults to disabled, rejects ambiguous structure and validates public-only key/callback policy before composing native services.</summary>
    internal static AdministratorRegistrationPolicy? Read(IConfiguration configuration, bool accounts, IssuerCredentials? credentials)
    {
        var section = configuration.GetSection(Section);
        if (section.Value is not null || section.GetChildren().Any(child => !Fields.Contains(child.Key) || child.GetChildren().Any()))
        {
            throw new AdministratorProtocolConfigurationException();
        }
        var enabled = section[Enabled];
        if (enabled is null || string.Equals(enabled, False, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (!string.Equals(enabled, True, StringComparison.OrdinalIgnoreCase) || !accounts || credentials is null ||
            section[Redirect] is not { } redirect || section[PublicKey] is not { } document ||
            AdministratorPublicKey.Parse(document) is not { } key || AdministratorRegistrationPolicy.Create(redirect, key) is not { } policy)
        {
            throw new AdministratorProtocolConfigurationException();
        }
        return policy;
    }
}

/// <summary>Reports a fixed diagnostic without exposing callback, key or other supplied configuration.</summary>
internal sealed class AdministratorProtocolConfigurationException : Exception
{
    private const string SafeMessage = "Invalid administrator protocol configuration. Explicit activation requires administrator accounts, issuer protection and the fixed public registration profile.";

    /// <summary>Creates only the finite startup diagnostic.</summary>
    internal AdministratorProtocolConfigurationException() : base(SafeMessage) { }
}
