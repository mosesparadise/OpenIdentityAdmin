using IdentityPlatform.AuthServer.Features.Accounts.Shared;

namespace IdentityPlatform.AuthServer.Features.Protocol.Protection;

/// <summary>Preserves the pilot diagnostic API while sharing the account secret-suppression policy.</summary>
/// <example><code>PilotDiagnostics.SuppressProtocolSecrets(services);</code></example>
public static class PilotDiagnostics
{
    /// <summary>Suppresses sensitive framework logs, including provider-specific reload overrides.</summary>
    /// <example><code>PilotDiagnostics.SuppressProtocolSecrets(services);</code></example>
    public static void SuppressProtocolSecrets(IServiceCollection services) => AccountDiagnostics.SuppressSecrets(services);

    /// <summary>Reports whether a logger category belongs to a sensitive framework namespace.</summary>
    /// <example><code>bool sensitive = PilotDiagnostics.Sensitive("OpenIddict.Server");</code></example>
    public static bool Sensitive(string? category) => AccountDiagnostics.Sensitive(category);
}
