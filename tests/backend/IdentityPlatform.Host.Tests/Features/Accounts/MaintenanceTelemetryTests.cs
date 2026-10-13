using System.Diagnostics.Metrics;
using IdentityPlatform.AuthServer.Features.Accounts.Maintenance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Isolates process-wide meters from other tests while checking the real host exporter composition.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AdministratorTelemetryIsolation
{
    public const string Name = "administrator-telemetry";
}

/// <summary>Proves feature-scoped subscriptions export only approved streams through the host-owned provider.</summary>
[Collection(AdministratorTelemetryIsolation.Name)]
public sealed class MaintenanceTelemetryTests
{
    private const string Prefix = "IdentityPlatform.Administrator.";
    private const string Ceremonies = "IdentityPlatform.Administrator.Ceremonies";
    private const string Cleanup = "IdentityPlatform.Administrator.Cleanup";
    private const string Canary = "IdentityPlatform.Administrator.Unapproved";
    private const string CanaryCounter = "unapproved.measurement";
    private const string Decisions = "administrator.ceremony.decisions";
    private const string Backlog = "administrator.cleanup.backlog";
    private const string Deferred = "administrator.cleanup.deferred";
    private const string OutcomeTag = "outcome";
    private const string CategoryTag = "category";
    private const int FlushMilliseconds = 5_000;
    private const long BacklogRows = 3;
    private static readonly string[] Outcomes = ["verified", "replay-denied", "capacity-denied", "locked"];
    private static readonly string[] Categories = ["interactions", "sessions", "browsers", "replays"];

    /// <summary>Only enabled administrator hosts export explicitly subscribed measurements; pilot and unrelated meters do not opt in.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_FeatureSwitches_When_AdministratorMeasurementsAreFlushed_Then_OnlyEnabledHostsExportSafeMetrics(bool pilot, bool administrator)
    {
        var exported = new List<Metric>();
        await using var host = await AccountHost.StartAsync(pilot, administrator, configureServices: services =>
        {
            services.AddSingleton<TimeProvider>(new NoScheduledTicks());
            services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddReader(
                new BaseExportingMetricReader(new InMemoryExporter<Metric>(exported))));
        });
        var provider = host.Services.GetRequiredService<MeterProvider>();
        using var canary = new Meter(Canary);
        canary.CreateCounter<long>(CanaryCounter).Add(1);
        foreach (var outcome in Enum.GetValues<CeremonyOutcome>())
        {
            CeremonyMetrics.Record(outcome);
        }
        foreach (var category in Categories)
        {
            CeremonyMetrics.RecordBacklog(category, BacklogRows);
        }
        await EmitDeferredAsync();
        Assert.True(provider.ForceFlush(FlushMilliseconds));
        var actual = exported.Where(metric => metric.MeterName.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
        if (!administrator)
        {
            Assert.Empty(actual);
            return;
        }
        Assert.Equal(3, actual.Length);
        RequirePoints(Assert.Single(actual, metric => metric.Name == Decisions), Ceremonies, MetricType.LongSum, OutcomeTag, Outcomes);
        RequirePoints(Assert.Single(actual, metric => metric.Name == Backlog), Ceremonies, MetricType.Histogram, CategoryTag, Categories);
        RequirePoints(Assert.Single(actual, metric => metric.Name == Deferred), Cleanup, MetricType.LongSum, null, []);
    }

    /// <summary>Uses the real failed-pass path with missing dependencies; it never fabricates a cleanup counter measurement.</summary>
    private static async Task EmitDeferredAsync()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var cleanup = new AdministratorCleanupService(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
        await cleanup.RunOnceAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Requires exact approved meter/type/tags and independent measurement values, rejecting extra dimensions.</summary>
    private static void RequirePoints(Metric metric, string meter, MetricType type, string? tagName, string[] expectedTags)
    {
        Assert.Equal(meter, metric.MeterName);
        Assert.Equal(type, metric.MetricType);
        var values = new List<string>();
        var count = 0;
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            count++;
            var tags = new List<KeyValuePair<string, object?>>();
            foreach (var tag in point.Tags)
            {
                tags.Add(tag);
            }
            if (tagName is null)
            {
                Assert.Empty(tags);
            }
            else
            {
                var tag = Assert.Single(tags);
                Assert.Equal(tagName, tag.Key);
                values.Add(Assert.IsType<string>(tag.Value));
            }
            if (type == MetricType.Histogram)
            {
                Assert.Equal(1, point.GetHistogramCount());
                Assert.Equal(BacklogRows, point.GetHistogramSum());
            }
            else
            {
                Assert.Equal(1, point.GetSumLong());
            }
        }
        Assert.Equal(tagName is null ? 1 : expectedTags.Length, count);
        Assert.Equal(expectedTags.Order(StringComparer.Ordinal), values.Order(StringComparer.Ordinal));
    }

    /// <summary>Disables scheduled callbacks only; production registrations and explicit measurement paths remain unchanged.</summary>
    private sealed class NoScheduledTicks : TimeProvider
    {
        /// <summary>Returns an owned dormant timer so the sixty-second cleanup schedule cannot race the explicit test pass.</summary>
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            System.CreateTimer(callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }
}
