namespace IdentityPlatform.AppHost.Features.Database;

internal sealed class DatabaseResources(
    string provider,
    IResourceBuilder<ParameterResource> platformPassword,
    IResourceBuilder<ParameterResource> sessionsPassword,
    IResourceBuilder<IResourceWithConnectionString> platform,
    IResourceBuilder<IResourceWithConnectionString> sessions,
    ReferenceExpression platformRuntime,
    ReferenceExpression sessionsRuntime)
{
    internal string Provider { get; } = provider;
    internal IResourceBuilder<ParameterResource> PlatformPassword { get; } = platformPassword;
    internal IResourceBuilder<ParameterResource> SessionsPassword { get; } = sessionsPassword;
    internal IResourceBuilder<IResourceWithConnectionString> Platform { get; } = platform;
    internal IResourceBuilder<IResourceWithConnectionString> Sessions { get; } = sessions;
    internal ReferenceExpression PlatformRuntime { get; } = platformRuntime;
    internal ReferenceExpression SessionsRuntime { get; } = sessionsRuntime;
}
