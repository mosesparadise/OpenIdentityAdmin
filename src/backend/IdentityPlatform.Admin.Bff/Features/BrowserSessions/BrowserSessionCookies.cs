namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Defines separate host-only browser cookie roles without taking ownership of OpenIddict's native correlation cookie.</summary>
internal static class BrowserSessionCookies
{
    internal const string Session = "__Host-identity-admin-session";
    internal const string Generation = "__Host-identity-admin-generation";
    internal const string Prelogin = "__Host-identity-admin-prelogin";
    internal const string Logout = "__Host-identity-admin-logout";
    internal const string Antiforgery = "__Host-identity-admin-antiforgery";
    internal const string GenerationPurpose = "IdentityPlatform.Admin.Bff.BrowserGeneration.v1";
    internal const string PreloginPurpose = "IdentityPlatform.Admin.Bff.BrowserPrelogin.v1";
    internal const string LogoutPurpose = "IdentityPlatform.Admin.Bff.BrowserLogout.v1";
    private const string Root = "/";

    /// <summary>Caps an operation/proof cookie by its actual server deadline, truncating to whole seconds and denying subsecond or expired publication.</summary>
    internal static CookieOptions? Bounded(DateTimeOffset now, DateTimeOffset deadline)
    {
        var seconds = (deadline.UtcTicks - now.UtcTicks) / TimeSpan.TicksPerSecond;
        if (deadline <= now || seconds <= 0)
        {
            return null;
        }
        var options = SessionOptions();
        options.MaxAge = TimeSpan.FromSeconds(seconds);
        return options;
    }

    /// <summary>Uses a nonpersistent session cookie; every acceptance still enforces server lifetime and durable generation without sliding.</summary>
    internal static CookieOptions SessionOptions() => new()
    {
        Path = Root,
        Domain = null,
        Secure = true,
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true
    };
}
