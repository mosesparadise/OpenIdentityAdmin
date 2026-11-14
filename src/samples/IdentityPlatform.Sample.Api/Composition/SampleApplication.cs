using IdentityPlatform.Sample.Api.Features.Orders;
using IdentityPlatform.Sample.Api.Features.Validation;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.Sample.Api.Composition;

/// <summary>Starts only the explicitly enabled disposable public-key resource server.</summary>
internal static class SampleApplication
{
    private const int FailureExitCode = 2;
    private const string NativeLogCategory = "OpenIddict";
    private const string AuthenticationLogCategory = "Microsoft.AspNetCore.Authentication";
    private const string HttpLogCategory = "System.Net.Http.HttpClient";
    private const string DiagnosticsLogCategory = "Microsoft.AspNetCore.Diagnostics";
    private const string NoStorePolicy = "no-store";

    /// <summary>Composes a secret-free API and reports only a fixed startup failure.</summary>
    internal static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);
            var app = Create(builder);
            await app.RunAsync();
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine(SampleProfile.Failure);
            return FailureExitCode;
        }
    }
    /// <summary>Builds the production native validation pipeline for Aspire and real HTTP/token regression tests.</summary>
    internal static WebApplication Create(WebApplicationBuilder builder)
    {
        SampleProfile.RequireDevelopment(builder.Configuration, builder.Environment);
        builder.AddPlatformServiceDefaults();
        builder.Logging.AddFilter(NativeLogCategory, LogLevel.None).AddFilter(AuthenticationLogCategory, LogLevel.None)
            .AddFilter(HttpLogCategory, LogLevel.None).AddFilter(DiagnosticsLogCategory, LogLevel.None);
        builder.Services.AddSampleValidation();
        var app = builder.Build();
        app.Use(BoundaryAsync);
        app.UseAuthentication();
        app.MapPlatformHealthEndpoints();
        app.MapOrders();
        return app;
    }

    /// <summary>Limits requests to the fixed HTTPS authority and turns internal validation failures into finite responses.</summary>
    private static async Task BoundaryAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.Headers.CacheControl = NoStorePolicy;
        if (!context.Request.IsHttps || context.Request.Host.Value != new Uri(SampleProfile.Api).Authority)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        deadline.CancelAfter(SampleProfile.OperationLifetime);
        context.RequestAborted = deadline.Token;
        using var abort = deadline.Token.Register(context.Abort);
        try
        {
            await next(context);
        }
        catch (Exception)
        {
            if (context.Response.HasStarted || deadline.IsCancellationRequested)
            {
                context.Abort();
            }
            else
            {
                context.Response.Clear();
                context.Response.Headers.CacheControl = NoStorePolicy;
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
        }
    }

}
