using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.Sample.Client.Features.Sessions;

/// <summary>Binds pending native callbacks to one browser independently of the authenticated ticket cookie.</summary>
internal sealed class AttemptCookie(IDataProtectionProvider protection)
{
    private const int ProtectedCookieLimit = 1024;
    private const int BrowserHandleLength = 43;
    private const string HomePath = "/";
    private const int CookieMinutes = 5;

    private const string Name = "__Host-sample-attempt";
    private readonly IDataProtector protector = protection.CreateProtector("IdentityPlatform.Sample.Attempt.v1");

    /// <summary>Accepts only a protected bounded browser handle, never arbitrary caller state.</summary>
    internal string? Read(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(Name, out var value) || value.Length > ProtectedCookieLimit)
        {
            return null;
        }
        try
        {
            var id = protector.Unprotect(value);
            return id.Length == BrowserHandleLength ? id : null;
        }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }

    /// <summary>Writes a host-only temporary browser binding without embedding protocol credentials.</summary>
    internal void Write(HttpContext context, string id) => context.Response.Cookies.Append(Name, protector.Protect(id), Options());

    /// <summary>Removes the pending browser binding after terminal logout.</summary>
    internal static void Clear(HttpContext context) => context.Response.Cookies.Delete(Name, Options());

    /// <summary>Restricts correlation to secure top-level same-site navigation and the original short attempt lifetime.</summary>
    private static CookieOptions Options() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = HomePath,
        MaxAge = TimeSpan.FromMinutes(CookieMinutes),
        IsEssential = true
    };
}
