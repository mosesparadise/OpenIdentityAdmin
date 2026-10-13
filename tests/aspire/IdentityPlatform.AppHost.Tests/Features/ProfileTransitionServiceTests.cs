using System.Runtime.CompilerServices;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.AppHost.Tests.Features;

[Trait("Category", "Harness")]
public sealed class ProfileTransitionServiceTests
{
    private const string Worker = "worker";
    private const int FailureExitCode = 2;
    private const string Canary = "synthetic-private-test-error";
    private static readonly TimeSpan TestDeadline = TimeSpan.FromSeconds(5);

    /// <summary>Consumes real Aspire seeded/live publications and terminates its observer before the service is disposed.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_RealNotificationService_When_SeededAndLiveEventsArePublished_Then_FirstUnavailableIsRetained(bool seed)
    {
        using var host = CreateHost();
        var service = host.Services.GetRequiredService<ResourceNotificationService>();
        var resource = new ProjectResource(Worker);
        if (seed)
        {
            await service.PublishUpdateAsync(resource, state => state with { State = new(KnownResourceStates.Starting, null) });
        }
        var consumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lines = new List<string>();
        var primary = new InvalidOperationException(Canary);
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => ProfileTransitionObserver.ObserveAsync(
            token => ReadAsync(service, consumed, token), async _ =>
            {
                await service.PublishUpdateAsync(resource, state => state with { State = new(KnownResourceStates.Finished, null), ExitCode = FailureExitCode });
                await consumed.Task.WaitAsync(TestDeadline, TestContext.Current.CancellationToken);
                throw primary;
            }, lines.Add).WaitAsync(TestDeadline, TestContext.Current.CancellationToken));
        Assert.Same(primary, observed);
        const string expectedFirst = "profile-first-unavailable role=worker";
        const string expectedExit = "state=Finished exit=2";
        const string expectedStarting = "state=Starting";
        Assert.Contains(lines, line => line.StartsWith(expectedFirst, StringComparison.Ordinal)
            && line.Contains(expectedExit, StringComparison.Ordinal));
        Assert.Contains("observer-failed=False", lines[0], StringComparison.Ordinal);
        if (seed)
        {
            Assert.Contains(lines, line => line.Contains(expectedStarting, StringComparison.Ordinal));
        }
    }

    /// <summary>Immediate failure preserves the primary exception without pretending queued publications were fully drained.</summary>
    [Fact]
    public async Task Given_QueuedPublication_When_ActionFailsImmediately_Then_PrimaryErrorAndUnconfirmedDrainAreRetained()
    {
        using var host = CreateHost();
        var service = host.Services.GetRequiredService<ResourceNotificationService>();
        var resource = new ProjectResource(Worker);
        await service.PublishUpdateAsync(resource, state => state with { State = new(KnownResourceStates.Starting, null) });
        var lines = new List<string>();
        var primary = new InvalidOperationException(Canary);
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => ProfileTransitionObserver.ObserveAsync(
            service.WatchAsync, async _ =>
            {
                await service.PublishUpdateAsync(resource, state => state with { State = new(KnownResourceStates.Finished, null), ExitCode = FailureExitCode });
                throw primary;
            }, lines.Add).WaitAsync(TestDeadline, TestContext.Current.CancellationToken));
        Assert.Same(primary, observed);
        Assert.Contains("joined=True publication-drain=not-confirmed", lines[0], StringComparison.Ordinal);
    }

    /// <summary>Creates the real pinned notification service without starting child resources or containers.</summary>
    private static IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ResourceLoggerService>();
        builder.Services.AddSingleton<ResourceNotificationService>();
        return builder.Build();
    }

    /// <summary>Signals only after the observer has consumed the real terminal publication and requested its next item.</summary>
    private static async IAsyncEnumerable<ResourceEvent> ReadAsync(ResourceNotificationService service, TaskCompletionSource consumed,
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var value in service.WatchAsync(token))
        {
            yield return value;
            if (value.Snapshot.ExitCode == FailureExitCode)
            {
                consumed.TrySetResult();
            }
        }
    }
}
