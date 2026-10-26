using IdentityPlatform.ServiceDefaults.Features.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace IdentityPlatform.ServiceDefaults;

/// <summary>Shares safe operational telemetry and health endpoints across supported host composition styles.</summary>
/// <example><code>builder.AddPlatformServiceDefaults(); app.MapPlatformHealthEndpoints();</code></example>
public static class Extensions
{
    private const string HostingDiagnostics = "Microsoft.AspNetCore.Hosting.Diagnostics";
    private const string DatabaseDiagnostics = "Microsoft.EntityFrameworkCore";
    private const string HealthDiagnostics = "Microsoft.Extensions.Diagnostics.HealthChecks";
    private const string SelfCheck = "self";
    private const string LivenessTag = "live";
    private const string ReadinessTag = "ready";
    private const string AlivePath = "/alive";
    private const string HealthPath = "/health";
    private const string OtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    /// <summary>Registers bounded operational health and telemetry on an application builder.</summary>
    /// <example><code>WebApplication.CreateBuilder(args).AddPlatformServiceDefaults();</code></example>
    public static TBuilder AddPlatformServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ConfigureLogging(builder.Logging);
        Register(builder.Services, builder.Configuration);
        return builder;
    }

    /// <summary>Applies the same safe logs, health and telemetry to explicitly configured generic web hosts.</summary>
    /// <example><code>Host.CreateDefaultBuilder(args).AddPlatformServiceDefaults();</code></example>
    public static IHostBuilder AddPlatformServiceDefaults(this IHostBuilder builder) => builder
        .ConfigureLogging(ConfigureLogging)
        .ConfigureServices((context, services) => Register(services, context.Configuration));

    /// <summary>Excludes request URLs and provider exceptions from operational logs in both hosting models.</summary>
    private static void ConfigureLogging(ILoggingBuilder logging)
    {
        // Request URLs and provider exception details are not application log events.
        logging.AddFilter(HostingDiagnostics, LogLevel.None);
        logging.AddFilter(DatabaseDiagnostics, LogLevel.None);
        logging.AddFilter(HealthDiagnostics, LogLevel.None);
    }

    /// <summary>Shares existing health and reviewed telemetry without adding retries to native security mutations.</summary>
    private static void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks().AddCheck(SelfCheck, () => HealthCheckResult.Healthy(), tags: [LivenessTag]);
        services.AddServiceDiscovery();
        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation())
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    // Expand with reviewed, feature-specific instrumentation only. Protocol
                    // query strings, identity data and database statements are not trace payloads.
                    options.Filter = context => context.Request.Path is var path &&
                        (path == AlivePath || path == HealthPath);
                })
                .AddProcessor(new OperationalTraceProcessor()));
        if (!string.IsNullOrWhiteSpace(configuration[OtlpEndpoint]))
        {
            services.AddOpenTelemetry().UseOtlpExporter();
        }
    }

    /// <summary>Maps only fixed liveness/readiness responses on either minimal or explicit endpoint routing.</summary>
    /// <example><code>app.UseEndpoints(endpoints => endpoints.MapPlatformHealthEndpoints());</code></example>
    public static TBuilder MapPlatformHealthEndpoints<TBuilder>(this TBuilder app) where TBuilder : IEndpointRouteBuilder
    {
        app.MapGet(AlivePath, (HealthCheckService health, HttpContext context) =>
            Respond(health, context, LivenessTag));
        app.MapGet(HealthPath, (HealthCheckService health, HttpContext context) =>
            Respond(health, context, ReadinessTag));
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
