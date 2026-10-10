using IdentityPlatform.AuthServer.Features.Issuer.Credentials;

namespace IdentityPlatform.AuthServer.Features.Pilot;

public static class PilotConfiguration
{
    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment, IssuerCredentials? credentials)
    {
        var value = configuration["Pilot:Enabled"];
        if (value is null || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || !environment.IsDevelopment() || credentials is null)
        {
            throw new PilotConfigurationException();
        }
        return true;
    }
}

public sealed class PilotConfigurationException : Exception
{
    public PilotConfigurationException() : base("Invalid first-party pilot configuration. Only an explicitly configured Development pilot is supported.") { }
}
