using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.AuthServer.Features.Accounts;

internal static class Setup
{
    internal static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        services.AddLogin();
        services.AddIdentityCore<PlatformUser>().AddSignInManager();
        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = 16;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        });
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
        services.AddAuthorization();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = PilotProfile.SessionCookie;
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromSeconds(PilotProfile.SessionSeconds);
            options.SlidingExpiration = false;
        });
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-pilot-antiforgery";
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            // Cross-site authorization GETs must reuse this cookie so another tab cannot replace it.
            // Native Razor antiforgery validation still protects every unsafe form submission.
            options.Cookie.SameSite = SameSiteMode.Lax;
        });
        services.Configure<FormOptions>(options =>
        {
            options.ValueCountLimit = 16;
            options.KeyLengthLimit = 128;
            options.ValueLengthLimit = 8192;
            options.MultipartBodyLengthLimit = 16_384;
        });
        services.AddRazorPages(options => options.RootDirectory = "/Features/Accounts");
        return services;
    }

    internal static WebApplication MapAccounts(this WebApplication app)
    {
        app.MapRazorPages();
        app.MapAccountAssets();
        return app;
    }
}
