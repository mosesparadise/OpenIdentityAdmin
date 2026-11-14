using IdentityPlatform.Sample.Client.Features.Authentication;
using IdentityPlatform.Sample.Client.Features.Configuration;
using IdentityPlatform.Sample.Client.Features.Presentation;
using IdentityPlatform.Sample.Client.Features.ResourceAccess;
using IdentityPlatform.Sample.Client.Features.Sessions;
using IdentityPlatform.ServiceDefaults;
using Microsoft.AspNetCore.Antiforgery;

namespace IdentityPlatform.Sample.Client.Composition;

/// <summary>Starts a Development-only sample whose native authentication remains uninitialized until private setup is ready.</summary>
internal static class SampleApplication
{
    private const string ConfigureArgument = "--configure";
    private const int FailureExitCode = 2;
    private const string NativeLogCategory = "OpenIddict";
    private const string AuthenticationLogCategory = "Microsoft.AspNetCore.Authentication";
    private const string HttpLogCategory = "System.Net.Http.HttpClient";
    private const string DiagnosticsLogCategory = "Microsoft.AspNetCore.Diagnostics";
    private const int MaximumRequestBytes = 8192;
    private const string HealthPath = "/health";
    private const string HealthyStatus = "Healthy";
    private const string HomePath = "/";

    /// <summary>Handles private stdin setup or composes the interactive host; startup errors never include input or credentials.</summary>
    internal static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var configure = args.SequenceEqual([ConfigureArgument]);
            var builder = WebApplication.CreateBuilder(configure ? [] : args);
            SampleProfile.RequireDevelopment(builder.Configuration, builder.Environment);
            if (configure)
            {
                using var deadline = new CancellationTokenSource(SampleProfile.OperationLifetime);
                await SampleConfiguration.ConfigureAsync(Console.OpenStandardInput(), deadline.Token);
                return 0;
            }
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

    /// <summary>Builds the same guarded native host for ordinary Aspire and in-memory HTTP regression tests.</summary>
    internal static WebApplication Create(WebApplicationBuilder builder)
    {
        SampleProfile.RequireDevelopment(builder.Configuration, builder.Environment);
        builder.AddPlatformServiceDefaults();
        builder.Logging.AddFilter(NativeLogCategory, LogLevel.None).AddFilter(AuthenticationLogCategory, LogLevel.None)
            .AddFilter(HttpLogCategory, LogLevel.None).AddFilter(DiagnosticsLogCategory, LogLevel.None);
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaximumRequestBytes);
        builder.Services.AddSampleAuthentication();
        builder.Services.AddHttpClient<SampleResources>(client => client.Timeout = SampleProfile.OperationLifetime)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.None });
        var app = builder.Build();
        app.Use(BoundaryAsync);
        app.UseAuthentication();
        app.Use(MutationAsync);
        app.MapPlatformHealthEndpoints();
        app.MapSample();
        return app;
    }

    /// <summary>Gates native middleware/options until private configuration exists and enforces origin/CSRF before mutations.</summary>
    private static async Task BoundaryAsync(HttpContext context, RequestDelegate next)
    {
        SamplePage.Headers(context);
        using var budget = new RequestBudget(context);
        try
        {
            if (!context.Request.IsHttps || context.Request.Host.Value != new Uri(SampleProfile.Client).Authority)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            if (context.Request.Path == HealthPath && HttpMethods.IsGet(context.Request.Method) && !context.Request.QueryString.HasValue)
            {
                await context.Response.WriteAsync(HealthyStatus, context.RequestAborted);
                return;
            }
            if (context.RequestServices.GetRequiredService<SampleConfiguration>().Read() is null)
            {
                if (context.Request.Path == HomePath && HttpMethods.IsGet(context.Request.Method))
                {
                    await SamplePage.WriteAsync(context, configured: false);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                }
                return;
            }
            await next(context);
        }
        catch (Exception)
        {
            if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested)
            {
                context.Abort();
                return;
            }
            context.Response.Clear();
            SamplePage.Headers(context);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await SamplePage.WriteFailureAsync(context);
        }
    }

    /// <summary>Validates browser mutations after cookie authentication so native antiforgery sees the original identity binding.</summary>
    private static async Task MutationAsync(HttpContext context, RequestDelegate next)
    {
        if (HttpMethods.IsPost(context.Request.Method))
        {
            if (context.Request.Headers.Origin.Count != 1 || context.Request.Headers.Origin[0] != SampleProfile.Client.TrimEnd('/'))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            try
            {
                await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
        }
        await next(context);
    }

}
