using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.AppHost.Features.LocalDemo;

/// <summary>Registers initialization on the exact run-owned resource inside the authenticated Aspire dashboard.</summary>
internal static class DashboardInitialization
{
    private const string ReadyState = "Ready";
    private const string UnavailableState = "Unavailable";

    private const string DisplayName = "Initialize administrator";
    private const string ContextFailure = "Initialization is unavailable for this resource or supplied arguments.";

    /// <summary>Retains callback state per resource rather than registering mutable credentials in the application model.</summary>
    internal static IResourceBuilder<ContainerResource> WithDashboardInitialization(this IResourceBuilder<ContainerResource> resource, string run, IDemoProcess? process = null)
    {
        var command = new DemoCommand(process ?? new DemoProcess(), run);
        return resource.WithCommand(DemoCommand.Name, DisplayName, context => ExecuteAsync(resource.Resource, command, context),
            new CommandOptions
            {
                UpdateState = context => command.Available && context.ResourceSnapshot.State?.Text == KnownResourceStates.Running
                    ? ResourceCommandState.Enabled : ResourceCommandState.Disabled
            });
    }

    /// <summary>Rechecks the resource independently of UI state and links cancellation to AppHost shutdown.</summary>
    private static async Task<ExecuteCommandResult> ExecuteAsync(ContainerResource resource, DemoCommand command, ExecuteCommandContext context)
    {
        if (context.Arguments.Count != 0)
        {
            return new ExecuteCommandResult { Success = false, Message = ContextFailure };
        }
        var services = context.Services;
        var notifications = services.GetRequiredService<ResourceNotificationService>();
        var lifetime = services.GetRequiredService<IHostApplicationLifetime>();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, lifetime.ApplicationStopping);
        return await command.ExecuteAsync(services.GetRequiredService<IInteractionService>(),
            () => notifications.TryGetCurrentState(context.ResourceName, out var current) && ReferenceEquals(current.Resource, resource) &&
                current.Snapshot.State?.Text == KnownResourceStates.Running, linked.Token,
            () => PublishStateAsync(notifications, resource, command, context.ResourceName));
    }

    /// <summary>Refreshes command availability without exposing input or letting notification failure alter mutation truth.</summary>
    private static async Task PublishStateAsync(ResourceNotificationService notifications, ContainerResource resource, DemoCommand command, string invocationId)
    {
        const string property = "local-demo-initialization";
        try
        {
            if (!notifications.TryGetCurrentState(invocationId, out var current) || !ReferenceEquals(current.Resource, resource))
            {
                return;
            }
            await notifications.PublishUpdateAsync(resource, current.ResourceId, snapshot => snapshot with
            {
                Properties = [.. snapshot.Properties.Where(value => value.Name != property), new ResourcePropertySnapshot(property, command.Available ? ReadyState : UnavailableState)]
            });
        }
        catch (Exception) { /* UI notification is advisory; callback admission remains authoritative. */ }
    }

}
