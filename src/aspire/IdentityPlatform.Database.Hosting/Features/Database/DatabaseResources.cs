using Aspire.Hosting.ApplicationModel;

namespace IdentityPlatform.Database.Hosting;

/// <summary>Returns the fixed logical database resources and least-privilege connection expressions to AppHost wiring.</summary>
/// <example><code>var resources = adapter.Add(builder, platformPassword, sessionsPassword);</code></example>
public sealed class DatabaseResources(
    IRelationalDatabaseProvider provider,
    IResourceBuilder<ParameterResource> platformPassword,
    IResourceBuilder<ParameterResource> sessionsPassword,
    IResourceBuilder<IResourceWithConnectionString> platform,
    IResourceBuilder<IResourceWithConnectionString> sessions,
    ReferenceExpression platformRuntime,
    ReferenceExpression sessionsRuntime)
{
    public IRelationalDatabaseProvider Registration { get; } = provider;
    public string Provider => Registration.Id;
    public IResourceBuilder<ParameterResource> PlatformPassword { get; } = platformPassword;
    public IResourceBuilder<ParameterResource> SessionsPassword { get; } = sessionsPassword;
    public IResourceBuilder<IResourceWithConnectionString> Platform { get; } = platform;
    public IResourceBuilder<IResourceWithConnectionString> Sessions { get; } = sessions;
    public ReferenceExpression PlatformRuntime { get; } = platformRuntime;
    public ReferenceExpression SessionsRuntime { get; } = sessionsRuntime;
}
