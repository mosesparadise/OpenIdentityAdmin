using System.Diagnostics.Metrics;
using IdentityPlatform.AuthServer.Features.Accounts.Maintenance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Verifies cleanup composition, fixed scheduling, safe failed-pass telemetry and cooperative shutdown.</summary>
public sealed class MaintenanceTests
{
    private const string MeterName = "IdentityPlatform.Administrator.Cleanup";
    private const string DeferredName = "administrator.cleanup.deferred";
    private const int IntervalSeconds = 60;
    private const int WaitSeconds = 5;

    /// <summary>Only administrator activation installs the cleanup scheduler; pilot activation does not opt into administrator maintenance.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_FeatureSwitches_When_HostStarts_Then_CleanupMatchesAdministratorActivation(bool pilot, bool administrator)
    {
        await using var host = await AccountHost.StartAsync(pilot, administrator);
        Assert.Equal(administrator ? 1 : 0, host.Services.GetServices<IHostedService>().OfType<AdministratorCleanupService>().Count());
    }

    /// <summary>A failed scheduled pass emits one untagged fixed counter and shutdown cancels the periodic wait.</summary>
    [Fact]
    public async Task Given_FailedCleanupDependency_When_ScheduledPassRuns_Then_OnlySafeTelemetryIsEmittedAndShutdownCompletes()
    {
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == MeterName && instrument.Name == DeferredName)
            {
                owner.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            Assert.Equal(1, value);
            Assert.True(tags.IsEmpty);
            observed.TrySetResult();
        });
        listener.Start();
        await using var provider = new ServiceCollection().BuildServiceProvider();
        using var service = new AdministratorCleanupService(provider.GetRequiredService<IServiceScopeFactory>(), new ImmediateTick());
        await service.StartAsync(TestContext.Current.CancellationToken);
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(WaitSeconds), TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Checks the production interval while delivering one immediate deterministic test tick.</summary>
    private sealed class ImmediateTick : TimeProvider
    {
        /// <summary>Runs exactly one scheduled callback and preserves normal periodic-timer cancellation semantics.</summary>
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.Equal(TimeSpan.FromSeconds(IntervalSeconds), dueTime);
            Assert.Equal(TimeSpan.FromSeconds(IntervalSeconds), period);
            return System.CreateTimer(callback, state, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }
}
