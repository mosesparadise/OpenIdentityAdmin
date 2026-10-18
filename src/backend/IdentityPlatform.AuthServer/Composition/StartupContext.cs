using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

namespace IdentityPlatform.AuthServer.Composition;

internal sealed class StartupContext(string[] arguments) : IDisposable
{
    internal bool InitializePilot { get; } = arguments.Contains("--initialize-pilot", StringComparer.Ordinal);
    internal string[] HostArguments { get; } = arguments.Where(value => value != "--initialize-pilot").ToArray();
    internal IssuerCredentials? Credentials
    {
        get; set;
    }
    internal bool PilotEnabled
    {
        get; set;
    }

    internal bool AdministratorEnabled
    {
        get; set;
    }

    internal AdministratorRegistrationPolicy? AdministratorProtocol
    {
        get; set;
    }

    // Program owns this context outside the application lifetime. DI must not own it.
    public void Dispose() => Credentials?.Dispose();
}
