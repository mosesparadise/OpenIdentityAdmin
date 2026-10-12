using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Isolates administrator account authority from pilot, protocol and management authentication.</summary>
internal static class AccountSchemes
{
    internal const string Administrator = "IdentityPlatform.Administrator";
    internal const string Anonymous = "IdentityPlatform.Accounts.None";
    internal const string AdministratorCookie = "__Host-identity-administrator";
    private const int AbsoluteHours = 8;

    /// <summary>Installs an explicit non-authenticating default and a fail-closed administrator cookie handler.</summary>
    internal static void AddAdministrator(IServiceCollection services, bool pilot)
    {
        var authentication = pilot ? services.AddAuthentication() : services.AddAuthentication(Anonymous);
        authentication.AddScheme<AuthenticationSchemeOptions, NoAccountAuthentication>(Anonymous, _ => { });
        services.AddScoped<AdministratorCookieEvents>();
        services.AddScoped<AdministratorTicketPermit>();
        authentication.AddCookie(Administrator, Configure);
    }

    /// <summary>Uses every-request durable validation and one-shot issuance with a nonpersistent absolute cookie lifetime.</summary>
    private static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = AdministratorCookie;
        options.Cookie.Path = AccountPolicy.Root;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(AbsoluteHours);
        options.SlidingExpiration = false;
        options.EventsType = typeof(AdministratorCookieEvents);
    }

}
