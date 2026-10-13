using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Reads one bounded native password form without logging, redisplaying or persisting submitted credentials.</summary>
internal sealed record AdministratorPasswordForm(string Username, string Password, string Browser, string Correlation)
{
    /// <summary>Rejects ambiguous or oversized input and generates only transient browser correlation candidates.</summary>
    internal static async Task<AdministratorPasswordForm?> ReadAsync(HttpContext context)
    {
        var form = await AdministratorBrowser.FormAsync(context, AdministratorBrowser.Username, AdministratorBrowser.Password);
        if (form is null || form[AdministratorBrowser.Username][0]!.Length > AdministratorBrowser.UsernameLength ||
            form[AdministratorBrowser.Password][0]!.Length > AdministratorBrowser.PasswordLength)
        {
            return null;
        }
        var browser = context.Request.Cookies[AdministratorBrowser.BudgetCookie];
        return new AdministratorPasswordForm(form[AdministratorBrowser.Username][0]!, form[AdministratorBrowser.Password][0]!,
            CeremonySecrets.IsIdentifier(browser) ? browser! : CeremonySecrets.Create(), CeremonySecrets.Create());
    }
}
