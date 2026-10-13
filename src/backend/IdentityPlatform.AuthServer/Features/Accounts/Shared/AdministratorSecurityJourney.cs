using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using Microsoft.AspNetCore.Authentication;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Releases only committed restricted transitions, replacement sessions and one-time code responses to protected Razor handlers.</summary>
internal sealed class AdministratorSecurityJourney(JourneyPassword passwords, JourneyTransition transition, JourneyCompletion completion,
    JourneyStaging staging, JourneyLogout logout, JourneyDenial denial, DummyPassword dummy, AdministratorTicketPermit tickets)
    : IAdministratorSecurityJourney
{
    /// <summary>Checks the exact password form and, for voluntary actions, supplies only a current cookie candidate to locked validation.</summary>
    public async Task<string?> BeginAsync(HttpContext context, AdministratorSecurityAction action)
    {
        var request = await AdministratorPasswordForm.ReadAsync(context);
        if (request is null)
        {
            return null;
        }
        var purpose = AdministratorSecurityBrowser.Purpose(action);
        var candidate = action == AdministratorSecurityAction.Recovery ? null : await AdministratorSecurityBrowser.CandidateAsync(context);
        if (action != AdministratorSecurityAction.Recovery && candidate is null)
        {
            return null;
        }
        var result = await passwords.BeginAsync(request.Username, request.Password, purpose, request.Browser, request.Correlation,
            context.RequestAborted, candidate);
        if (result.Interaction is null)
        {
            if (result.NeedsDummyWork)
            {
                dummy.Verify(request.Password);
                await denial.WriteAsync(context.RequestAborted);
            }
            return null;
        }
        context.RequestAborted.ThrowIfCancellationRequested();
        AdministratorBrowser.Bind(context, result.Interaction, request.Browser, request.Correlation);
        var path = action switch
        {
            AdministratorSecurityAction.Recovery => AdministratorSecurityBrowser.RecoveryVerifyPath,
            AdministratorSecurityAction.Replacement => AdministratorSecurityBrowser.ReplacementVerifyPath,
            _ => AdministratorSecurityBrowser.RegenerationVerifyPath
        };
        return AdministratorSecurityBrowser.Location(path, result.Interaction);
    }

    /// <summary>Rotates the opaque browser binding only after actual native step-up and the audited restricted transition commit.</summary>
    public async Task<string?> AuthorizeAsync(HttpContext context, bool recovery)
    {
        var request = await AdministratorSecurityBrowser.CodeAsync(context, recovery);
        var browser = context.Request.Cookies[AdministratorBrowser.BudgetCookie];
        if (request is not { } form || !CeremonySecrets.IsIdentifier(browser))
        {
            return null;
        }
        var candidate = recovery ? null : await AdministratorSecurityBrowser.CandidateAsync(context);
        var correlation = CeremonySecrets.Create();
        var publicId = await transition.AuthorizeAsync(form.Interaction, AdministratorBrowser.Correlation(context, form.Interaction),
            recovery ? AdministratorPolicy.Recovery : AdministratorPolicy.Replacement, form.Code, correlation, context.RequestAborted, candidate);
        if (publicId is null)
        {
            return null;
        }
        context.RequestAborted.ThrowIfCancellationRequested();
        AdministratorBrowser.Bind(context, publicId, browser!, correlation);
        return AdministratorSecurityBrowser.Location(AdministratorSecurityBrowser.ConfirmPath, publicId);
    }

    /// <summary>Obtains the unforgeable postcommit grant before signing or exposing any new recovery-code material.</summary>
    public async Task<IReadOnlyList<string>?> CompleteAsync(HttpContext context, bool regeneration)
    {
        var request = await AdministratorSecurityBrowser.CodeAsync(context, recovery: false);
        if (request is not { } form)
        {
            return null;
        }
        var candidate = await AdministratorSecurityBrowser.CandidateAsync(context);
        var grant = await completion.CompleteAsync(form.Interaction, AdministratorBrowser.Correlation(context, form.Interaction),
            regeneration ? AdministratorPolicy.Regeneration : AdministratorPolicy.Replacement, form.Code, context.RequestAborted, candidate);
        if (grant is null)
        {
            return null;
        }
        await tickets.IssueAsync(context, grant);
        return Array.AsReadOnly(grant.RecoveryCodes);
    }

    /// <summary>Supplies cookie identifiers only as candidates; the staging service owns current-session and restricted-proof checks.</summary>
    public async Task<string?> StagedAsync(HttpContext context)
    {
        var publicId = AdministratorBrowser.Locator(context);
        return publicId is null ? null : await staging.ReadAsync(publicId, AdministratorBrowser.Correlation(context, publicId),
            context.RequestAborted, AdministratorPolicy.Replacement, await AdministratorSecurityBrowser.CandidateAsync(context));
    }

    /// <summary>Accepts only the antiforgery-only form and clears the named cookie after exact-session revocation and audit succeed.</summary>
    public async Task<bool> LogoutAsync(HttpContext context)
    {
        if (await AdministratorBrowser.FormAsync(context) is null ||
            !await logout.RevokeAsync(await AdministratorSecurityBrowser.CandidateAsync(context), context.RequestAborted))
        {
            return false;
        }
        context.RequestAborted.ThrowIfCancellationRequested();
        await context.SignOutAsync(AccountSchemes.Administrator);
        return true;
    }
}
