using System.Globalization;
using System.Security.Claims;
using IdentityPlatform.Sample.Client.Features.Presentation;
using IdentityPlatform.Sample.Client.Features.ResourceAccess;
using IdentityPlatform.Sample.Client.Features.Sessions;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Client.AspNetCore;

namespace IdentityPlatform.Sample.Client.Features.Authentication;

/// <summary>Coordinates native challenges/callbacks with the sample's browser-owned publication and local logout boundary.</summary>
internal static class SampleSignIn
{
    private const string HomePath = "/";
    private const string SignInDeniedMessage = "Sign-in was not completed. Start a fresh sign-in.";
    private const int SubjectLimit = 256;
    private const int AccessTokenLimit = 16384;
    private const string SignedOutMessage = "Signed out of this sample browser. Other applications and browsers are unchanged.";

    internal const string BrowserProperty = "sample.browser";
    internal const string AccessToken = "access_token";
    private const string NativeScheme = OpenIddictClientAspNetCoreDefaults.AuthenticationScheme;

    /// <summary>Ends old local authority before creating one new native code/S256 challenge and browser binding.</summary>
    internal static async Task LoginAsync(HttpContext context)
    {
        var attempts = context.RequestServices.GetRequiredService<BrowserAttempts>();
        var cookie = context.RequestServices.GetRequiredService<AttemptCookie>();
        using (var old = await attempts.EnterAsync(cookie.Read(context), context.RequestAborted))
        {
            old?.End();
            await context.SignOutAsync(SampleTickets.Scheme);
        }
        var browser = attempts.Begin();
        using var lease = await attempts.EnterAsync(browser, context.RequestAborted) ?? throw new InvalidOperationException(SampleProfile.Failure);
        try
        {
            context.Features.Get<RequestBudget>()!.RestrictTo(lease.Expires);
            cookie.Write(context, browser);
            var properties = new AuthenticationProperties { RedirectUri = HomePath };
            properties.Items[BrowserProperty] = browser;
            properties.Items[OpenIddictClientAspNetCoreConstants.Properties.RegistrationId] = SampleProfile.Registration;
            await RequestBudget.AwaitAsync(context.ChallengeAsync(NativeScheme, properties), context.RequestAborted);
            await RequestBudget.AwaitAsync(context.Response.StartAsync(context.RequestAborted), context.RequestAborted);
            await RequestBudget.AwaitAsync(context.Response.CompleteAsync(), context.RequestAborted);
        }
        catch
        {
            lease.End();
            throw;
        }
    }

    /// <summary>Accepts only a successful native identity and matching pending browser, then publishes an isolated server-held session.</summary>
    internal static async Task CallbackAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(NativeScheme);
        var browser = context.RequestServices.GetRequiredService<AttemptCookie>().Read(context);
        using var lease = await context.RequestServices.GetRequiredService<BrowserAttempts>().EnterAsync(browser, context.RequestAborted);
        if (!result.Succeeded || result.Principal is null || result.Properties is null || lease is null || !lease.Pending ||
            !result.Properties.Items.TryGetValue(BrowserProperty, out var returned) || returned != browser)
        {
            await SamplePage.WriteAsync(context, configured: true, result: new ResourceResult(SignInDeniedMessage));
            return;
        }
        try
        {
            await PublishSessionAsync(context, result, browser!, lease);
        }
        catch
        {
            lease.End();
            if (context.Response.HasStarted)
            {
                context.Abort();
            }
            else
            {
                context.Response.Clear();
            }
            throw;
        }
    }

    /// <summary>Publishes native cookie authority only within the original request and token/attempt expiry budget.</summary>
    private static async Task PublishSessionAsync(HttpContext context, AuthenticateResult result, string browser, BrowserAttempts.BrowserLease lease)
    {
        var properties = SessionProperties(result.Properties!, lease.Expires);
        context.Features.Get<RequestBudget>()!.RestrictTo(properties.ExpiresUtc!.Value);
        var subjects = result.Principal!.FindAll(OpenIddictConstants.Claims.Subject).Select(claim => claim.Value).ToArray();
        if (subjects.Length != 1 || subjects[0].Length is 0 or > SubjectLimit)
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        var identity = new ClaimsIdentity(SampleTickets.Scheme);
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, subjects[0]));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subjects[0]));
        identity.AddClaim(new Claim(SampleTickets.BrowserClaim, browser!));
        lease.Authenticate(properties.ExpiresUtc!.Value);
        await RequestBudget.AwaitAsync(context.SignInAsync(SampleTickets.Scheme, new ClaimsPrincipal(identity), properties), context.RequestAborted);
        context.Response.Redirect(HomePath);
        await RequestBudget.AwaitAsync(context.Response.StartAsync(context.RequestAborted), context.RequestAborted);
        await RequestBudget.AwaitAsync(context.Response.CompleteAsync(), context.RequestAborted);
    }

    /// <summary>Caps server tickets by the authenticated native token response's absolute expiration and the original attempt bound.</summary>
    private static AuthenticationProperties SessionProperties(AuthenticationProperties native, DateTimeOffset attemptExpires)
    {
        var accessToken = native.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken);
        var expiration = native.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessTokenExpirationDate);
        if (accessToken is not { Length: > 0 and <= AccessTokenLimit } ||
            !DateTimeOffset.TryParse(expiration, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires))
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        var properties = new AuthenticationProperties { ExpiresUtc = expires < attemptExpires ? expires : attemptExpires, AllowRefresh = false, IsPersistent = false };
        properties.StoreTokens([new AuthenticationToken { Name = AccessToken, Value = accessToken }]);
        return properties;
    }

    /// <summary>Serializes logout with admitted output and destroys pending callback authority before confirming local completion.</summary>
    internal static async Task LogoutAsync(HttpContext context)
    {
        var cookie = context.RequestServices.GetRequiredService<AttemptCookie>();
        using var lease = await context.RequestServices.GetRequiredService<BrowserAttempts>().EnterAsync(cookie.Read(context), context.RequestAborted);
        lease?.End();
        await context.SignOutAsync(SampleTickets.Scheme);
        AttemptCookie.Clear(context);
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        await SamplePage.WriteAsync(context, configured: true, result: new ResourceResult(SignedOutMessage));
    }
}
