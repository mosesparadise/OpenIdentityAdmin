using IdentityPlatform.AuthServer.Features.Accounts.Enrollment;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Accounts.Maintenance;
using IdentityPlatform.AuthServer.Features.Accounts.Mfa;
using IdentityPlatform.AuthServer.Features.Accounts.Recovery;
using IdentityPlatform.AuthServer.Features.Accounts.Security;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.AuthServer.Features.Accounts;

/// <summary>Composes enabled account families once, independently of protocol registration.</summary>
internal static class Setup
{
    /// <summary>Registers common protections and only the requested authentication schemes and page families.</summary>
    internal static IServiceCollection AddAccounts(this IServiceCollection services, bool pilot, bool administrator)
    {
        services.AddIdentityCore<PlatformUser>().AddSignInManager();
        services.Configure<IdentityOptions>(AccountOptions.Identity);
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = AccountPolicy.PasswordIterations);
        services.AddAuthorization();
        services.AddSingleton<DummyPassword>();
        services.AddAntiforgery(AccountOptions.Antiforgery);
        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(AccountOptions.Form);
        services.AddRazorPages(options =>
        {
            options.RootDirectory = AccountPolicy.PageRoot;
            options.Conventions.Add(new AccountPageConvention(pilot, administrator));
        });
        if (pilot)
        {
            AddPilot(services);
        }
        if (administrator)
        {
            AccountSchemes.AddAdministrator(services, pilot);
            services.AddAdministratorMaintenance();
            services.AddAdministratorJourneys();
            services.AddScoped<IAdministratorJourney, AdministratorJourney>();
            services.AddScoped<IAdministratorSecurityJourney, AdministratorSecurityJourney>();
            services.AddEnrollment().AddMfa().AddRecovery().AddAccountSecurity();
        }
        return services.AddAccountProtection();
    }

    /// <summary>Preserves the pilot's existing scheme, lifetime and default without forwarding administrator tickets.</summary>
    private static void AddPilot(IServiceCollection services)
    {
        services.AddLogin();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = PilotProfile.SessionCookie;
            options.Cookie.Path = AccountPolicy.Root;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromSeconds(PilotProfile.SessionSeconds);
            options.SlidingExpiration = false;
        });
    }

    /// <summary>Maps only selectors retained by the explicit account convention and fixed embedded theme assets.</summary>
    internal static WebApplication MapAccounts(this WebApplication app)
    {
        app.MapRazorPages();
        app.MapAccountAssets();
        return app;
    }
}
