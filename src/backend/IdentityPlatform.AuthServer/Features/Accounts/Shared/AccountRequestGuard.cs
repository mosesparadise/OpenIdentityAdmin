using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Shares bounded request handling and secret-safe failures without coupling accounts to pilot handlers.</summary>
internal static class AccountRequestGuard
{
    private const string TooLarge = "The sign-in request is too large.";
    private const string Invalid = "The sign-in request is invalid.";
    private const string Unavailable = "Sign-in is temporarily unavailable. Return to your application and try again.";
    private const string IncidentHeader = "X-Identity-Incident";
    private const string GuidFormat = "N";
    private const string ReferrerHeader = "Referrer-Policy";
    private static readonly System.Text.CompositeFormat Csp = System.Text.CompositeFormat.Parse(
        "default-src 'none'; script-src 'nonce-{0}'; style-src 'self'; img-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'self'");

    /// <summary>Applies transport and database bounds; converts failures before response start to fixed, secret-free responses.</summary>
    internal static async Task InvokeAsync(HttpContext context, RequestDelegate next, Action<string> failed)
    {
        if (Operational(context))
        {
            await next(context);
            return;
        }
        ApplyBodyLimit(context);
        SetHeaders(context);
        if (context.Request.ContentLength > AccountPolicy.BodyBytes)
        {
            await WriteAsync(context, StatusCodes.Status413PayloadTooLarge, TooLarge);
            return;
        }
        try
        {
            var database = context.RequestServices.GetRequiredService<PlatformDbContext>();
            AccountDatabaseBounds.Apply(database);
            await next(context);
        }
        catch (BadHttpRequestException exception) when (!context.Response.HasStarted)
        {
            await WriteAsync(context, exception.StatusCode, Invalid);
        }
        catch (Exception) when (!context.Response.HasStarted)
        {
            var incident = Guid.NewGuid().ToString(GuidFormat);
            failed(incident);
            context.Response.Clear();
            SetHeaders(context);
            context.Response.Headers[IncidentHeader] = incident;
            await WriteAsync(context, StatusCodes.Status503ServiceUnavailable, Unavailable);
        }
    }

    /// <summary>Excludes liveness/readiness and fixed static assets from account-specific processing.</summary>
    private static bool Operational(HttpContext context) => context.Request.Path == AccountPolicy.Alive ||
        context.Request.Path == AccountPolicy.Health || context.Request.Path.StartsWithSegments(AccountPolicy.AssetsPrefix);

    /// <summary>Caps readable request bodies before downstream form parsing.</summary>
    private static void ApplyBodyLimit(HttpContext context)
    {
        var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = AccountPolicy.BodyBytes;
        }
    }

    /// <summary>Sets a fresh nonce and non-caching browser policy, including on safe failure responses.</summary>
    private static void SetHeaders(HttpContext context)
    {
        const string NoStore = "no-store";
        const string NoCache = "no-cache";
        const string NoReferrer = "no-referrer";
        const string NoSniff = "nosniff";
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(AccountPolicy.NonceBytes));
        context.Items[AccountPolicy.NonceItem] = nonce;
        context.Response.Headers.CacheControl = NoStore;
        context.Response.Headers.Pragma = NoCache;
        context.Response.Headers.XContentTypeOptions = NoSniff;
        context.Response.Headers[ReferrerHeader] = NoReferrer;
        context.Response.Headers.ContentSecurityPolicy = string.Format(System.Globalization.CultureInfo.InvariantCulture, Csp, nonce);
    }

    /// <summary>Completes a classified response without writing request-derived values.</summary>
    private static Task WriteAsync(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = AccountPolicy.TextContentType;
        return context.Response.WriteAsync(message, CancellationToken.None);
    }
}
