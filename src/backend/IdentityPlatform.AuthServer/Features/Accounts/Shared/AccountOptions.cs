using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Shares existing account identity, antiforgery and form policies without enabling an authentication journey.</summary>
internal static class AccountOptions
{
    /// <summary>Applies the approved password and lockout baseline; administrator combined budgets remain a later feature.</summary>
    internal static void Identity(IdentityOptions options)
    {
        options.Password.RequiredLength = AccountPolicy.PasswordLength;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = AccountPolicy.FailedAttempts;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(AccountPolicy.LockoutMinutes);
    }

    /// <summary>Preserves the host-only antiforgery cookie used across account tabs and requires native POST validation.</summary>
    internal static void Antiforgery(AntiforgeryOptions options)
    {
        options.Cookie.Name = AccountPolicy.AntiforgeryCookie;
        options.Cookie.Path = AccountPolicy.Root;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    }

    /// <summary>Bounds native form parsing independently of the overall body cap.</summary>
    internal static void Form(FormOptions options)
    {
        options.ValueCountLimit = AccountPolicy.FormValues;
        options.KeyLengthLimit = AccountPolicy.FormKeyLength;
        options.ValueLengthLimit = AccountPolicy.FormValueLength;
        options.MultipartBodyLengthLimit = AccountPolicy.BodyBytes;
    }
}
