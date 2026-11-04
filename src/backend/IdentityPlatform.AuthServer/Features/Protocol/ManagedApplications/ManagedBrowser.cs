using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Protects an issuer-only browser correlation value with a separate purpose; it carries no user or session authority.</summary>
internal static class ManagedBrowser
{
    internal const string Cookie = "__Host-managed-browser";
    private const string CookiePath = "/";
    private const string Purpose = "IdentityPlatform.ManagedApplications.Browser.v1";
    private const int MaximumCookieLength = 1024;

    /// <summary>Reads only a well-shaped protected browser proof; malformed cookies cannot substitute administrator session state.</summary>
    internal static string? Read(HttpContext http)
    {
        var value = http.Request.Cookies[Cookie];
        if (value is not { Length: > 0 and <= MaximumCookieLength })
        {
            return null;
        }
        try
        {
            var proof = http.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Unprotect(value);
            return ManagedLoginRules.IsSecret(proof) ? proof : null;
        }
        catch (CryptographicException) { return null; }
    }

    /// <summary>Sets one protected host-only proof in the staged cache response; no identity cookie or completion capability is issued.</summary>
    internal static string GetOrCreate(HttpContext http)
    {
        var proof = Read(http) ?? ManagedLoginRules.CreateSecret();
        var value = http.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect(proof);
        http.Response.Cookies.Append(Cookie, value, new CookieOptions
        {
            Secure = true,
            HttpOnly = true,
            Path = CookiePath,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromSeconds(ManagedLoginRules.LifetimeSeconds)
        });
        return proof;
    }
}
