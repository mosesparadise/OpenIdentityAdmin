using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using IdentityPlatform.AppHost.Features.LocalDemo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityPlatform.AppHost.Tests.Features.DemoInitialization;

/// <summary>Executes the registered production callback with real notification/lifetime services and controlled transport.</summary>
internal sealed class DashboardFixture : IDisposable
{
    internal const string ResourceName = "initializer";
    internal const string InvocationId = "initializer-native-id";
    private const string UnusedImage = "unused";
    private readonly IHost host;
    internal DemoInteraction Interaction { get; } = new();
    internal DemoProcessWitness Process { get; } = new();
    internal ContainerResource Resource
    {
        get;
    }
    internal ResourceCommandAnnotation Command
    {
        get;
    }
    internal ResourceNotificationService Notifications => host.Services.GetRequiredService<ResourceNotificationService>();

    /// <summary>Builds only host services; no Docker resource is started by these wrapper controls.</summary>
    internal DashboardFixture()
    {
        var builder = DistributedApplication.CreateBuilder();
        Resource = builder.AddContainer(ResourceName, UnusedImage).WithDashboardInitialization(DemoProcessWitness.Run, Process).Resource;
        Command = Assert.Single(Resource.Annotations.OfType<ResourceCommandAnnotation>(), command => command.Name == DemoCommand.Name);
        var services = Host.CreateApplicationBuilder();
        services.Services.AddSingleton<ResourceLoggerService>();
        services.Services.AddSingleton<ResourceNotificationService>();
        services.Services.AddSingleton<IInteractionService>(Interaction);
        host = services.Build();
    }

    /// <summary>Publishes native aliases, preserving the original resource object that the callback must authenticate.</summary>
    internal Task PublishAsync(ContainerResource resource, string id, string state) =>
        Notifications.PublishUpdateAsync(resource, id, snapshot => snapshot with { State = new(state, null) });

    /// <summary>Dispatches the actual registered callback, never a duplicated test implementation.</summary>
    internal Task<ExecuteCommandResult> ExecuteAsync(string id = InvocationId, InteractionInputCollection? arguments = null) =>
        Command.ExecuteCommand(new ExecuteCommandContext
        {
            ResourceName = id,
            Services = host.Services,
            CancellationToken = TestContext.Current.CancellationToken,
            Logger = NullLogger.Instance,
            Arguments = arguments ?? new InteractionInputCollection([])
        });

    /// <summary>Reads the current production availability callback against a real published snapshot.</summary>
    internal ResourceCommandState State()
    {
        Assert.True(Notifications.TryGetCurrentState(InvocationId, out var current));
        return Command.UpdateState(new UpdateCommandStateContext { ResourceSnapshot = current.Snapshot, Services = host.Services });
    }

    /// <summary>Signals real host shutdown while leaving an independent cleanup lifetime available.</summary>
    internal void StopHost() => host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

    /// <summary>Disposes only the in-process host services owned by this fixture.</summary>
    public void Dispose() => host.Dispose();
}
