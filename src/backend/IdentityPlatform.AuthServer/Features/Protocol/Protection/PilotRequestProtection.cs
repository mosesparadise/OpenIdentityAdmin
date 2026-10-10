using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.AuthServer.Features.Protocol.Protection;

public sealed class PilotRequestProtection(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/alive" || context.Request.Path == "/health" || context.Request.Path.StartsWithSegments("/assets"))
        {
            await next(context);
            return;
        }
        var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = 16_384;
        }
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        context.Items["CspNonce"] = nonce;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Content-Security-Policy"] =
            $"default-src 'none'; script-src 'nonce-{nonce}'; style-src 'self'; img-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'self'";
        if (context.Request.ContentLength > 16_384)
        {
            context.Response.StatusCode = 413;
            await context.Response.WriteAsync("The sign-in request is too large.", context.RequestAborted);
            return;
        }
        var database = context.RequestServices.GetRequiredService<PlatformDbContext>();
        PilotDatabaseBounds.Apply(database);
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException exception) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = exception.StatusCode;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("The sign-in request is invalid.", CancellationToken.None);
        }
        catch (Exception) when (!context.Response.HasStarted)
        {
            // Never emit exception objects: provider/framework messages may include sensitive input.
            context.Response.Clear();
            context.Response.StatusCode = 503;
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("Sign-in is temporarily unavailable. Return to your application and try again.", CancellationToken.None);
        }
    }
}
