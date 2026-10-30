using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Offers explicit dormant product login and callback composition while leaving production Programs unchanged.</summary>
internal static class Setup
{
    private const int CaptureOffset = 250;
    private const string CookiePath = "/";
    internal const string InvalidComposition = "Browser login requires explicit compatible middleware composition.";

    /// <summary>Uses caller-owned durable protection and native client services; no fallback keys, provider switches or runtime activation flags are added.</summary>
    internal static IServiceCollection AddBrowserLogin(this IServiceCollection services, BrowserSessionDestinations destinations, SessionProtection protection)
    {
        services.AddSingleton(destinations);
        services.AddSingleton(protection.Provider);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(new SessionTicketCodec(protection.Provider));
        services.AddScoped<BrowserLogoutProof>();
        services.AddScoped<BrowserLoginProof>().AddScoped<BrowserLoginService>().AddScoped<BrowserSessionReader>().AddScoped<BrowserLogoutHandler>();
        services.AddSingleton<IAntiforgeryAdditionalDataProvider, BrowserAntiforgeryData>();
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = BrowserSessionCookies.Antiforgery;
            options.Cookie.Path = CookiePath;
            options.Cookie.Domain = null;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.FormFieldName = BrowserRequestPolicy.AntiforgeryField;
            options.HeaderName = BrowserRequestPolicy.JsonAntiforgeryHeader;
        });
        services.AddOpenIddict().AddClient().AddEventHandler<OpenIddictClientEvents.ProcessChallengeContext>(handler => handler
            .UseSingletonHandler<BrowserLoginChallenge>().SetOrder(OpenIddictClientHandlers.AttachResponseMode.Descriptor.Order + CaptureOffset));
        services.AddOpenIddict().AddClient().UseAspNetCore().EnableRedirectionEndpointPassthrough();
        services.AddOpenIddict().AddClient().AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(handler => handler
            .UseSingletonHandler<BrowserCallbackAdmissionHandler>().SetOrder(OpenIddictClientHandlers.SendTokenRequest.Descriptor.Order - CaptureOffset));
        services.AddOpenIddict().AddClient().AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(handler => handler
            .UseSingletonHandler<BrowserCallbackIdentityHandler>().SetOrder(OpenIddictClientHandlers.PopulateMergedPrincipal.Descriptor.Order - IdentityOffset));
        return services;
    }

    private const int IdentityOffset = 125;

    /// <summary>Terminates exact login paths in explicitly composed hosts; automatic minimal-host authentication needs its later reviewed activation adapter.</summary>
    internal static IApplicationBuilder UseBrowserLogin(this IApplicationBuilder app)
    {
        if (app is WebApplication)
        {
            throw new InvalidOperationException(InvalidComposition);
        }
        RequireProfile(app.ApplicationServices);
        var factory = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>();
        var clock = app.ApplicationServices.GetRequiredService<TimeProvider>();
        var callback = app.ApplicationServices.GetRequiredService<NativeClientProfile>().Callback.AbsolutePath;
        app.Use((http, next) => http.Request.Path == BrowserLoginBoundary.Login || http.Request.Path == BrowserLogoutPage.Path || http.Request.Path == BrowserSessionDestinations.ResumePath || http.Request.Path == BrowserSessionError.Path
            ? BrowserLoginBoundary.RunAsync(http, factory, clock) : http.Request.Path == BrowserSessionDestinations.LogoutReturnPath
                ? BrowserLogoutReturn.RunAsync(http, factory, clock) : http.Request.Path == BrowserSessionBoundary.Path
                ? BrowserSessionBoundary.RunAsync(http, factory, clock) : http.Request.Path == callback
                ? BrowserCallbackBoundary.RunAsync(http, factory, clock) : next(http));
        return app;
    }
    /// <summary>Rejects divergent fixed identities, ordinary registrations and callbacks that could intercept product start/resume.</summary>
    private static void RequireProfile(IServiceProvider services)
    {
        var profile = services.GetRequiredService<NativeClientProfile>();
        var destinations = services.GetRequiredService<BrowserSessionDestinations>();
        var callback = Uri.UnescapeDataString(profile.Callback.AbsolutePath).TrimEnd('/');
        if (!profile.Registration.IsBrowserSession || profile.Issuer.AbsoluteUri != destinations.Issuer.AbsoluteUri ||
            profile.ClientUri.AbsoluteUri != destinations.Client.AbsoluteUri ||
            string.Equals(callback, BrowserLoginBoundary.Login, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(callback, BrowserLogoutPage.Path, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(callback, BrowserSessionDestinations.LogoutReturnPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(callback, BrowserSessionDestinations.ResumePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(callback, BrowserSessionBoundary.Path, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(callback, BrowserSessionError.Path, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(InvalidComposition);
        }
    }
}
