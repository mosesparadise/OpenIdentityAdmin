using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Owns bounded browser bindings and exact form shapes for administrator pages.</summary>
internal static class AdministratorBrowser
{
    internal const string BudgetCookie = "__Host-administrator-budget";
    internal const string CorrelationPrefix = "__Host-administrator-interaction-";
    internal const string Username = "Username";
    internal const string Password = "Password";
    internal const string Interaction = "Interaction";
    internal const string Code = "Code";
    internal const string Antiforgery = "__RequestVerificationToken";
    internal const string EnrollmentPath = "/account/administrator/enrollment";
    internal const string LoginPath = "/account/administrator/login";
    internal const string MfaPath = "/account/administrator/mfa";
    internal const string SecurityPath = "/account/administrator/security";
    internal const string Denied = "Unable to complete this request. Check your details or start again.";
    internal const int UsernameLength = 128;
    internal const int PasswordLength = 256;

    /// <summary>Rejects extra, duplicate, missing or query-supplied security values before invoking native account mechanisms.</summary>
    internal static async Task<IFormCollection?> FormAsync(HttpContext context, params string[] fields)
    {
        if (context.Request.Query.Count != 0 || !context.Request.HasFormContentType)
        {
            return null;
        }
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var allowed = fields.Append(Antiforgery).ToHashSet(StringComparer.Ordinal);
        return form.Files.Count == 0 && form.Count == allowed.Count && form.Keys.All(allowed.Contains) &&
            allowed.All(field => form[field].Count == 1 && !string.IsNullOrEmpty(form[field][0])) ? form : null;
    }

    /// <summary>Requires an exact single canonical locator on GET, or no query for the initial password form.</summary>
    internal static string? Locator(HttpContext context) => context.Request.Query.Count == 1 &&
        context.Request.Query[Interaction].Count == 1 && CeremonySecrets.IsIdentifier(context.Request.Query[Interaction][0])
        ? context.Request.Query[Interaction][0] : null;

    /// <summary>Reads the interaction-specific binding so one tab cannot silently overwrite another pending journey.</summary>
    internal static string Correlation(HttpContext context, string publicId) => CeremonySecrets.IsIdentifier(publicId)
        ? context.Request.Cookies[CorrelationPrefix + publicId] ?? string.Empty : string.Empty;

    /// <summary>Writes only secure, nonpersistent host cookies after interaction admission commits.</summary>
    internal static void Bind(HttpContext context, string publicId, string budget, string correlation)
    {
        var options = new CookieOptions
        {
            Path = AccountPolicy.Root,
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromSeconds(AdministratorPolicy.InteractionSeconds),
            IsEssential = true
        };
        context.Response.Cookies.Append(BudgetCookie, budget, options);
        context.Response.Cookies.Append(CorrelationPrefix + publicId, correlation, options);
    }
}
