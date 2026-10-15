using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace IdentityPlatform.Database.Hosting;

/// <summary>Rejects ambiguous or unusable local adapters before creating resources.</summary>
/// <example><code>var adapter = new DatabaseHostingProviders(projectAdapter).Select(configuredProvider);</code></example>
public sealed class DatabaseHostingProviders
{
    private const string InvalidAdapter = "Local database hosting requires a unique matching provider and provisioning capability.";
    private readonly DatabaseProviders providers;
    private readonly Dictionary<string, IDatabaseHostingProvider> adapters;

    /// <summary>Snapshots adapter registrations and validates their runtime identity and privileged capability.</summary>
    /// <example><code>var adapters = new DatabaseHostingProviders(projectAdapter);</code></example>
    public DatabaseHostingProviders(params IDatabaseHostingProvider[] registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        if (registrations.Any(adapter => adapter?.Provider?.LocalProvisioning is null))
        {
            throw new ArgumentException(InvalidAdapter);
        }
        providers = new DatabaseProviders(registrations.Select(adapter => adapter.Provider).ToArray());
        adapters = registrations.ToDictionary(adapter => adapter.Provider.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Validates returned resource identity before child wiring or application startup.</summary>
    /// <example><code>var resources = adapters.Add(providerId, builder, platformPassword, sessionsPassword);</code></example>
    public DatabaseResources Add(string? identifier, IDistributedApplicationBuilder builder,
        IResourceBuilder<ParameterResource> platformPassword, IResourceBuilder<ParameterResource> sessionsPassword)
    {
        var adapter = Select(identifier);
        var resources = adapter.Add(builder, platformPassword, sessionsPassword);
        if (!ReferenceEquals(resources.Registration, adapter.Provider))
        {
            throw new InvalidOperationException(InvalidAdapter);
        }
        return resources;
    }

    /// <summary>Selects the exact canonical registration with no default-adapter fallback.</summary>
    /// <example><code>var adapter = adapters.Select(configuration["Database:Provider"]);</code></example>
    public IDatabaseHostingProvider Select(string? identifier)
    {
        var selected = providers.Select(identifier);
        var adapter = adapters[selected.Id];
        if (!ReferenceEquals(selected, adapter.Provider))
        {
            throw new InvalidOperationException(InvalidAdapter);
        }
        return adapter;
    }
}
