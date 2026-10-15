using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace IdentityPlatform.Database.Hosting;

/// <summary>Optional local container adapter bound to an explicitly registered runtime provider.</summary>
/// <example><code>var resources = adapter.Add(builder, platformPassword, sessionsPassword);</code></example>
public interface IDatabaseHostingProvider
{
    IRelationalDatabaseProvider Provider
    {
        get;
    }

    /// <summary>Creates local database resources using caller-owned secret parameters.</summary>
    /// <example><code>var resources = adapter.Add(builder, platformPassword, sessionsPassword);</code></example>
    DatabaseResources Add(IDistributedApplicationBuilder builder, IResourceBuilder<ParameterResource> platformPassword,
        IResourceBuilder<ParameterResource> sessionsPassword);
}
