using System.Security.Claims;
using IdentityPlatform.Sample.Client.Features.Authentication;
using IdentityPlatform.Sample.Client.Features.ResourceAccess;
using IdentityPlatform.Sample.Client.Features.Sessions;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Sample.Client.Features.Presentation;

/// <summary>Maps finite interactive sample routes while keeping resource work and final publication separately authorized.</summary>
internal static class Endpoints
{
    private const string HomePath = "/";
    private const string CallbackPath = "/callback";
    private const string LoginPath = "/login";
    private const string LogoutPath = "/logout";
    private const string ProfilePath = "/profile";
    private const string OrdersPath = "/orders";
    private const string ExpiredSessionMessage = "Session expired. Sign in again.";
    private const string EndedSessionMessage = "Session ended before this result could be shown.";

    /// <summary>Maps only fixed actions; no generic proxy, configurable URL or public setup route exists.</summary>
    internal static void MapSample(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(HomePath, HomeAsync);
        endpoints.MapGet(CallbackPath, SampleSignIn.CallbackAsync);
        endpoints.MapPost(LoginPath, SampleSignIn.LoginAsync);
        endpoints.MapPost(LogoutPath, SampleSignIn.LogoutAsync);
        endpoints.MapPost(ProfilePath, context => AccessAsync(context, profile: true));
        endpoints.MapPost(OrdersPath, context => AccessAsync(context, profile: false));
    }

    /// <summary>Shows only an authenticated session whose browser authority and server ticket remain current.</summary>
    private static async Task HomeAsync(HttpContext context)
    {
        var browser = context.User.FindFirst(SampleTickets.BrowserClaim)?.Value;
        using var lease = await context.RequestServices.GetRequiredService<BrowserAttempts>().EnterAsync(browser, context.RequestAborted);
        var ticket = browser is not null && lease is { Pending: false } ? context.RequestServices.GetRequiredService<SampleTickets>().FindBrowser(browser) : null;
        if (lease is not null)
        {
            context.Features.Get<RequestBudget>()!.RestrictTo(lease.Expires);
        }
        if (ticket is null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }
        await SamplePage.WriteAsync(context, configured: true, ticket?.Principal.GetClaim(OpenIddictConstants.Claims.Subject));
    }

    /// <summary>Calls the fixed resource outside publication ownership, then rechecks the original session before any profile/result output.</summary>
    private static async Task AccessAsync(HttpContext context, bool profile)
    {
        var browser = context.User.FindFirst(SampleTickets.BrowserClaim)?.Value;
        var tickets = context.RequestServices.GetRequiredService<SampleTickets>();
        var ticket = browser is null ? null : tickets.FindBrowser(browser);
        var subject = ticket?.Principal.GetClaim(OpenIddictConstants.Claims.Subject);
        var token = ticket?.Properties.GetTokenValue(SampleSignIn.AccessToken);
        if (browser is null || subject is null || token is null || ticket?.Properties.ExpiresUtc is not { } expires)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await SamplePage.WriteAsync(context, configured: true, result: new ResourceResult(ExpiredSessionMessage));
            return;
        }
        context.Features.Get<RequestBudget>()!.RestrictTo(expires);
        var resources = context.RequestServices.GetRequiredService<SampleResources>();
        var result = profile ? await resources.ProfileAsync(token, subject, context.RequestAborted) : await resources.OrdersAsync(token, context.RequestAborted);
        using var lease = await context.RequestServices.GetRequiredService<BrowserAttempts>().EnterAsync(browser, context.RequestAborted);
        if (lease is null || lease.Pending || tickets.FindBrowser(browser) is null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await SamplePage.WriteAsync(context, configured: true, result: new ResourceResult(EndedSessionMessage));
            return;
        }
        await SamplePage.WriteAsync(context, configured: true, subject, result);
    }
}
