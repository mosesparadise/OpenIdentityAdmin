using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using IdentityPlatform.ServiceDefaults.Features.Telemetry;

namespace IdentityPlatform.Host.Tests.Features.Telemetry;

public sealed class OperationalTraceTests
{
    [Fact]
    public void Only_explicit_operational_tags_survive_export()
    {
        using var activity = new Activity("GET /health").Start();
        activity.TraceStateString = "vendor=synthetic-secret";
        activity.SetTag("url.full", "https://host/health?code=synthetic-secret");
        activity.SetTag("exception.message", "password=synthetic-secret");
        activity.SetTag("db.statement", "synthetic-secret");
        activity.SetTag("http.response.status_code", 503);
        activity.SetTag("http.request.method", "GET");
        activity.SetTag("http.route", "/health");
        new OperationalTraceProcessor().OnEnd(activity);
        Assert.Null(activity.TraceStateString);
        Assert.Null(activity.GetTagItem("url.full"));
        Assert.Null(activity.GetTagItem("exception.message"));
        Assert.Null(activity.GetTagItem("db.statement"));
        Assert.Equal(503, activity.GetTagItem("http.response.status_code"));
        Assert.Empty(activity.Events);
    }
    [Theory]
    [InlineData("none", 1)]
    [InlineData("event", 0)]
    [InlineData("baggage", 0)]
    [InlineData("link", 0)]
    public void Exporter_receives_sanitized_spans_or_suppresses_unsafe_immutable_payloads(string payload, int expected)
    {
        var exported = new List<Activity>();
        var name = "test-" + Guid.NewGuid();
        using var source = new ActivitySource(name);
        using var tracer = Sdk.CreateTracerProviderBuilder().AddSource(name)
            .AddProcessor(new OperationalTraceProcessor()).AddInMemoryExporter(exported).Build();
        using (var span = source.StartActivity("GET /health?code=synthetic-secret"))
        {
            Assert.NotNull(span);
            span.TraceStateString = "vendor=synthetic-secret";
            span.SetTag("url.full", "https://host/health?code=synthetic-secret");
            span.SetTag("http.route", "/health");
            span.SetTag("http.response.status_code", 503);
            span.SetStatus(ActivityStatusCode.Error, "synthetic-secret");
            if (payload == "event") span.AddEvent(new ActivityEvent("synthetic-secret"));
            if (payload == "baggage") span.AddBaggage("secret", "synthetic-secret");
            if (payload == "link") span.AddLink(new ActivityLink(new ActivityContext(
                ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded),
                new ActivityTagsCollection { ["secret"] = "synthetic-secret" }));
        }
        Assert.Equal(expected, exported.Count);
        if (expected == 1)
        {
            var span = Assert.Single(exported);
            Assert.Null(span.TraceStateString);
            Assert.Null(span.StatusDescription);
            Assert.Equal("GET /health", span.DisplayName);
            Assert.DoesNotContain(span.TagObjects, tag => tag.Value?.ToString()?.Contains("synthetic-secret") == true);
            Assert.Equal(503, span.GetTagItem("http.response.status_code"));
        }
    }
}
