using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Reserves a separate administrator scheme without granting authority before durable session validation exists.</summary>
internal static class AccountSchemes
{
    internal const string Administrator = "IdentityPlatform.Administrator";
    internal const string Anonymous = "IdentityPlatform.Accounts.None";
    internal const string AdministratorCookie = "__Host-identity-administrator";
    private const int AbsoluteHours = 8;
    private const string Unavailable = "Administrator session issuance is unavailable.";

    /// <summary>Installs an explicit non-authenticating default and a fail-closed administrator cookie handler.</summary>
    internal static void AddAdministrator(IServiceCollection services, bool pilot)
    {
        var authentication = pilot ? services.AddAuthentication() : services.AddAuthentication(Anonymous);
        authentication.AddScheme<AuthenticationSchemeOptions, NoAccountAuthentication>(Anonymous, _ => { });
        authentication.AddCookie(Administrator, Configure);
    }

    /// <summary>Keeps protected tickets unusable until T025 supplies authoritative session issuance and validation.</summary>
    private static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = AdministratorCookie;
        options.Cookie.Path = AccountPolicy.Root;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(AbsoluteHours);
        options.SlidingExpiration = false;
        options.Events.OnValidatePrincipal = RejectAsync;
        options.Events.OnSigningIn = _ => throw new InvalidOperationException(Unavailable);
        options.Events.OnRedirectToLogin = context => DenyAsync(context, StatusCodes.Status401Unauthorized);
        options.Events.OnRedirectToAccessDenied = context => DenyAsync(context, StatusCodes.Status403Forbidden);
    }

    /// <summary>Rejects even correctly protected tickets because no authoritative administrator session store exists yet.</summary>
    private static Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        return Task.CompletedTask;
    }

    /// <summary>Returns a status without redirecting to pilot pages or unimplemented administrator journeys.</summary>
    private static Task DenyAsync(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    }
}
