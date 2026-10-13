namespace IdentityPlatform.Governance.Driver.Features.Protocol;

/// <summary>Accepts only the exact synthetic baseline issuer configuration needed by governance process tests.</summary>
internal static class DriverIssuerSettings
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "Identity:Issuer", "Identity:Protection:DataProtection:KeyRingPath",
        "Identity:Protection:Signing:Path", "Identity:Protection:Signing:Password",
        "Identity:Protection:Encryption:Path", "Identity:Protection:Encryption:Password",
        "Identity:Protection:DataProtection:Current:Path", "Identity:Protection:DataProtection:Current:Password"
    };

    /// <summary>Requires each exact known key once, without case aliases, null values or oversized credential inputs.</summary>
    internal static void RequireAllowed(Dictionary<string, string?> settings)
    {
        if (!Allowed.SetEquals(settings.Keys) ||
            settings.Any(pair => pair.Value is null || pair.Value.Length > DriverLimits.SecretCharacters))
        {
            throw new InvalidOperationException();
        }
    }
}
