using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Registers the shared bounded account pipeline exactly once per account composition.</summary>
internal static class AccountProtection
{
    /// <summary>Installs request deadlines, admission limits and diagnostic suppression without enabling protocol routes.</summary>
    internal static IServiceCollection AddAccountProtection(this IServiceCollection services)
    {
        services.AddRequestTimeouts(options => options.DefaultPolicy = new()
        {
            Timeout = TimeSpan.FromSeconds(AccountPolicy.RequestSeconds),
            TimeoutStatusCode = StatusCodes.Status503ServiceUnavailable
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteDenialAsync;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(Window),
                PartitionedRateLimiter.Create<HttpContext, string>(Concurrency));
        });
        AccountDiagnostics.SuppressSecrets(services);
        return services;
    }

    /// <summary>Places safe error handling before admission and downstream account processing.</summary>
    internal static WebApplication UseAccountProtection(this WebApplication app, bool pilot)
    {
        if (pilot)
        {
            app.UseMiddleware<Protocol.Protection.PilotRequestProtection>();
        }
        else
        {
            app.UseMiddleware<AccountRequestProtection>();
        }
        app.UseStatusCodePages(AccountPolicy.TextContentType, AccountPolicy.StatusMessage);
        app.UseRateLimiter();
        return app;
    }

    /// <summary>Limits account/protocol traffic while excluding operational paths.</summary>
    private static RateLimitPartition<string> Window(HttpContext context) => Protected(context)
        ? RateLimitPartition.GetFixedWindowLimiter(AccountPolicy.Partition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = AccountPolicy.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }) : RateLimitPartition.GetNoLimiter(AccountPolicy.OperationalPartition);

    /// <summary>Rejects excess concurrent account work without a resource-retaining queue.</summary>
    private static RateLimitPartition<string> Concurrency(HttpContext context) => Protected(context)
        ? RateLimitPartition.GetConcurrencyLimiter(AccountPolicy.Partition, _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = AccountPolicy.ConcurrentRequests,
            QueueLimit = 0
        }) : RateLimitPartition.GetNoLimiter(AccountPolicy.OperationalPartition);

    /// <summary>Identifies account and protocol paths independently of which feature supplied the endpoint.</summary>
    private static bool Protected(HttpContext context) => context.Request.Path.StartsWithSegments(AccountPolicy.AccountPrefix) ||
        context.Request.Path.StartsWithSegments(AccountPolicy.ProtocolPrefix);

    /// <summary>Returns a fixed denial without request data or exception details.</summary>
    private static async ValueTask WriteDenialAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        context.HttpContext.Response.ContentType = AccountPolicy.TextContentType;
        await context.HttpContext.Response.WriteAsync(AccountPolicy.RateDenied, cancellationToken);
    }
}
