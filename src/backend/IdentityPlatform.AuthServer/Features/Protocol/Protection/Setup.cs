using System.Threading.RateLimiting;

namespace IdentityPlatform.AuthServer.Features.Protocol.Protection;

internal static class Setup
{
    internal static IServiceCollection AddPilotProtection(this IServiceCollection services)
    {
        services.AddRequestTimeouts(options => options.DefaultPolicy = new() { Timeout = TimeSpan.FromSeconds(10), TimeoutStatusCode = 503 });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                await context.HttpContext.Response.WriteAsync("Too many sign-in requests. Wait a minute and try again.", cancellationToken);
            };
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/connect") || context.Request.Path.StartsWithSegments("/account")
                    ? RateLimitPartition.GetFixedWindowLimiter("pilot", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 600,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    })
                    : RateLimitPartition.GetNoLimiter("operational")),
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    context.Request.Path.StartsWithSegments("/connect") || context.Request.Path.StartsWithSegments("/account")
                        ? RateLimitPartition.GetConcurrencyLimiter("pilot", _ => new ConcurrencyLimiterOptions { PermitLimit = 16, QueueLimit = 0 })
                        : RateLimitPartition.GetNoLimiter("operational")));
        });
        PilotDiagnostics.SuppressProtocolSecrets(services);
        return services;
    }

    internal static WebApplication UsePilotProtection(this WebApplication app)
    {
        app.UseMiddleware<PilotRequestProtection>();
        app.UseStatusCodePages("text/plain; charset=utf-8", "This request cannot be completed. Return to your application and start a new sign-in.");
        app.UseRateLimiter();
        return app;
    }
}
