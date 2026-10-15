namespace IdentityPlatform.Core.Features.Configuration;

/// <summary>Canonicalizes configuration against explicitly registered identifiers without vendor knowledge.</summary>
/// <example><code>var id = DatabaseProviderParser.Parse(configured, registeredIds);</code></example>
public static class DatabaseProviderParser
{
    private const string InvalidProvider = "Database:Provider must identify a registered database provider.";

    /// <summary>Requires exactly one registered match and never echoes rejected configuration.</summary>
    /// <example><code>var id = DatabaseProviderParser.Parse(configurationValue, registeredIds);</code></example>
    public static string Parse(string? value, IReadOnlyCollection<string> registeredIds)
    {
        ArgumentNullException.ThrowIfNull(registeredIds);
        var matches = registeredIds.Where(id => !string.IsNullOrWhiteSpace(id) &&
            string.Equals(id, value?.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new ArgumentException(InvalidProvider);
    }
}
