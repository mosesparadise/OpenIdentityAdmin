namespace IdentityPlatform.Database;

/// <summary>Snapshots explicit provider instances and resolves only uniquely registered canonical identifiers.</summary>
/// <example><code>var catalog = new DatabaseProviders(new ProjectDatabaseProvider());
/// var provider = catalog.Select(configuration["Database:Provider"]);</code></example>
public sealed class DatabaseProviders
{
    private const string InvalidRegistration = "Invalid or duplicate database provider registration.";
    private const string UnknownProvider = "Database:Provider must identify a registered database provider.";
    private readonly Dictionary<string, IRelationalDatabaseProvider> providers;

    /// <summary>Rejects empty catalogs, invalid identifiers and duplicates before any driver is configured.</summary>
    /// <example><code>var catalog = new DatabaseProviders(new ProjectDatabaseProvider());</code></example>
    public DatabaseProviders(params IRelationalDatabaseProvider[] registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var snapshot = new Dictionary<string, IRelationalDatabaseProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var registration in registrations)
        {
            if (registration is null || string.IsNullOrWhiteSpace(registration.Id) ||
                registration.Id != registration.Id.Trim() || !snapshot.TryAdd(registration.Id, registration))
            {
                throw new ArgumentException(InvalidRegistration);
            }
        }
        if (snapshot.Count == 0)
        {
            throw new ArgumentException(InvalidRegistration);
        }
        providers = snapshot;
    }

    /// <summary>Selects a canonical registered instance without logging or reflecting the supplied configuration.</summary>
    /// <example><code>var provider = catalog.Select(configuration["Database:Provider"]);</code></example>
    public IRelationalDatabaseProvider Select(string? identifier) =>
        identifier is not null && providers.TryGetValue(identifier.Trim(), out var provider)
            ? provider : throw new ArgumentException(UnknownProvider);
}
