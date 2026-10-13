using System.Security.Claims;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using Microsoft.AspNetCore.Authentication;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Owns fixed security routes and minimal cookie candidates without treating them as mutation authorization.</summary>
internal static class AdministratorSecurityBrowser
{
    internal const string RecoveryPath = "/account/administrator/recovery";
    internal const string RecoveryVerifyPath = "/account/administrator/recovery/verify";
    internal const string ReplacementPath = "/account/administrator/security/replacement";
    internal const string ReplacementVerifyPath = "/account/administrator/security/replacement/verify";
    internal const string ConfirmPath = "/account/administrator/security/replacement/confirm";
    internal const string RegenerationPath = "/account/administrator/security/recovery-codes";
    internal const string RegenerationVerifyPath = "/account/administrator/security/recovery-codes/verify";
    internal const string LogoutPath = "/account/administrator/logout";
    private const string Query = "?Interaction=";

    /// <summary>Resolves a server-selected action without accepting arbitrary protocol or account purpose strings.</summary>
    internal static string Purpose(AdministratorSecurityAction action) => action switch
    {
        AdministratorSecurityAction.Recovery => AdministratorPolicy.Recovery,
        AdministratorSecurityAction.Replacement => AdministratorPolicy.Replacement,
        AdministratorSecurityAction.Regeneration => AdministratorPolicy.Regeneration,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    /// <summary>Builds a fixed same-origin continuation containing only an opaque locator.</summary>
    internal static string Location(string path, string interaction) => path + Query + interaction;

    /// <summary>Reads the named cookie through its validator; sensitive operations still revalidate these candidates under lock.</summary>
    internal static async Task<JourneySessionCandidate?> CandidateAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(AccountSchemes.Administrator);
        return result.Succeeded && result.Principal is { } principal
            ? new JourneySessionCandidate(principal.FindFirstValue(ClaimTypes.NameIdentifier)!,
                principal.FindFirstValue(AdministratorTicketPermit.SessionClaim)!) : null;
    }

    /// <summary>Requires an exact canonical interaction/code form before any native action or secret-dependent work.</summary>
    internal static async Task<(string Interaction, string Code)?> CodeAsync(HttpContext context, bool recovery)
    {
        var form = await AdministratorBrowser.FormAsync(context, AdministratorBrowser.Interaction, AdministratorBrowser.Code);
        if (form is null || !CeremonySecrets.IsIdentifier(form[AdministratorBrowser.Interaction][0]))
        {
            return null;
        }
        var code = form[AdministratorBrowser.Code][0]!;
        const int RecoveryLength = 32;
        var valid = recovery ? code.Length == RecoveryLength && code.All(value => value is >= '0' and <= '9' or >= 'A' and <= 'F')
            : code.Length == AdministratorPolicy.CodeDigits && code.All(value => value is >= '0' and <= '9');
        return valid ? (form[AdministratorBrowser.Interaction][0]!, code) : null;
    }
}
