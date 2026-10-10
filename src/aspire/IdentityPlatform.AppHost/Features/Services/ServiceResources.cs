namespace IdentityPlatform.AppHost.Features.Services;

internal sealed class ServiceResources(IResourceBuilder<ProjectResource> auth, IResourceBuilder<ProjectResource> migrator)
{
    internal IResourceBuilder<ProjectResource> Auth { get; } = auth;
    internal IResourceBuilder<ProjectResource> Migrator { get; } = migrator;
}
