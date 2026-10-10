using IdentityPlatform.ServiceDefaults.Features.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace IdentityPlatform.ServiceDefaults;

public static class Extensions
{
    public static TBuilder AddPlatformServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Request URLs and provider exception details are not application log events.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.Extensions.Diagnostics.HealthChecks", LogLevel.None);
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);
        builder.Services.AddServiceDiscovery();
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation())
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    // Expand with reviewed, feature-specific instrumentation only. Protocol
                    // query strings, identity data and database statements are not trace payloads.
                    options.Filter = context => context.Request.Path is var path &&
                        (path == "/alive" || path == "/health");
                })
                .AddProcessor(new OperationalTraceProcessor()));
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }
        // No default HttpClient retries: security mutations must select their own safe policy.
        return builder;
    }

    public static WebApplication MapPlatformHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/alive", (HealthCheckService health, HttpContext context) =>
            Respond(health, context, "live"));
        app.MapGet("/health", (HealthCheckService health, HttpContext context) =>
            Respond(health, context, "ready"));
        return app;
    }

    private static async Task<IResult> Respond(HealthCheckService health, HttpContext context, string tag)
    {
        context.Response.Headers.CacheControl = "no-store";
        var report = await health.CheckHealthAsync(registration => registration.Tags.Contains(tag),
            context.RequestAborted);
        return Results.Text(report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy",
            statusCode: report.Status == HealthStatus.Healthy ? 200 : 503);
    }
}
