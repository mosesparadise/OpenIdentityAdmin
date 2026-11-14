using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.AppHost.Features.LocalDemo;

/// <summary>Adds independent disposable sample hosts and an authenticated one-shot credential configuration command.</summary>
internal static class SampleSetup
{
    private const string ApiHost = "orders.dev.localhost";
    private const int ApiPort = 17447;
    private const string ApiAuthority = "orders.dev.localhost:17447";
    private const string ClientHost = "sample.dev.localhost";
    private const int ClientPort = 17446;
    private const string ClientAuthority = "sample.dev.localhost:17446";
    private const string ContextFailureMessage = "Sample configuration is unavailable for supplied arguments.";
    private const string ReadyStatus = "Ready";
    private const string UnavailableStatus = "Unavailable";

    private const string Client = "sample-client";
    private const string Api = "sample-api";
    private const string Command = "configure-sample";
    private const string Display = "Configure sample client";

    /// <summary>Reuses ordinary local TLS/run ownership while keeping sample API/client free of platform database credentials.</summary>
    internal static void AddSample(this IDistributedApplicationBuilder builder, IResourceBuilder<ContainerResource> issuer, string run)
    {
        var api = builder.AddDemoContainer(Api, Api, run).WithDemoHttps(ApiHost, ApiPort).WithSampleProbeHost(ApiAuthority).WaitFor(issuer);
        var client = builder.AddDemoContainer(Client, Client, run).WithDemoHttps(ClientHost, ClientPort).WithSampleProbeHost(ClientAuthority).WaitFor(issuer).WaitFor(api);
        client.WithSampleConfiguration(run);
    }

    /// <summary>Attaches the real guarded dashboard callback to one client resource with an inspectable process boundary.</summary>
    internal static IResourceBuilder<ContainerResource> WithSampleConfiguration(this IResourceBuilder<ContainerResource> client, string run, IDemoProcess? process = null)
    {
        var command = new SampleCommand(process ?? new DemoProcess(), run);
        return client.WithCommand(Command, Display, async context =>
        {
            if (context.Arguments.Count != 0)
            {
                return new ExecuteCommandResult { Success = false, Message = ContextFailureMessage };
            }
            var notifications = context.Services.GetRequiredService<ResourceNotificationService>();
            var stopping = context.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, stopping);
            return await command.ExecuteAsync(context.Services.GetRequiredService<IInteractionService>(),
                () => notifications.TryGetCurrentState(context.ResourceName, out var current) && ReferenceEquals(current.Resource, client.Resource) &&
                    current.Snapshot.State?.Text == KnownResourceStates.Running, linked.Token,
                () => PublishAsync(notifications, client.Resource, command, context.ResourceName));
        }, new CommandOptions
        {
            UpdateState = context => command.Available && context.ResourceSnapshot.State?.Text == KnownResourceStates.Running
                ? ResourceCommandState.Enabled : ResourceCommandState.Disabled
        });
    }
    /// <summary>Publishes finite advisory command availability without allowing a dashboard failure to change setup truth.</summary>
    private static async Task PublishAsync(ResourceNotificationService notifications, ContainerResource resource, SampleCommand command, string invocation)
    {
        const string property = "local-demo-sample-setup";
        try
        {
            if (!notifications.TryGetCurrentState(invocation, out var current) || !ReferenceEquals(current.Resource, resource))
            {
                return;
            }
            await notifications.PublishUpdateAsync(resource, current.ResourceId, snapshot => snapshot with
            {
                Properties = [.. snapshot.Properties.Where(value => value.Name != property), new ResourcePropertySnapshot(property, command.Available ? ReadyStatus : UnavailableStatus)]
            });
        }
        catch (Exception) { /* Admission remains authoritative when advisory publication is unavailable. */ }
    }

    /// <summary>Keeps native loopback TLS probing while supplying the fixed canonical HTTP authority required by each sample host.</summary>
    private static IResourceBuilder<ContainerResource> WithSampleProbeHost(this IResourceBuilder<ContainerResource> resource, string authority)
    {
        var probe = resource.Resource.Annotations.OfType<HealthCheckAnnotation>().Single();
        resource.ApplicationBuilder.Services.AddHttpClient(probe.Key, client => client.DefaultRequestHeaders.Host = authority);
        return resource;
    }

}
