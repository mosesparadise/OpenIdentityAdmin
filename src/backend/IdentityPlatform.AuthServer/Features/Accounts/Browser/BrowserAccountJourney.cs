using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace IdentityPlatform.AuthServer.Features.Accounts.Browser;

/// <summary>Requires new native password/MFA and releases a fixed completion return only after real account commit and disposed ownership.</summary>
internal sealed class BrowserAccountJourney(BrowserAccountProfile profile, BrowserAccountProof proofs, TimeProvider clock,
    IssuerContinuationService continuations, JourneyPassword passwords, JourneyCompletion completion,
    DummyPassword dummy, JourneyDenial denial, AdministratorTicketPermit tickets)
{
    /// <summary>Admits or reconciles one original pending entry, then performs actual password work without consulting an existing issuer cookie.</summary>
    internal async Task<string?> BeginAsync(HttpContext context)
    {
        var form = await BrowserAccountRequest.FormAsync(context, profile, BrowserAccountProfile.Intent, AdministratorBrowser.Username, AdministratorBrowser.Password);
        if (form is null || form[AdministratorBrowser.Username][0]!.Length > AdministratorBrowser.UsernameLength ||
            form[AdministratorBrowser.Password][0]!.Length > AdministratorBrowser.PasswordLength ||
            proofs.Read(context, form[BrowserAccountProfile.Intent][0]!) is not { } entry ||
            !await continuations.AdmitForBrowserAsync(entry.Request!, entry.Expires, context.RequestAborted))
        {
            return null;
        }
        var budget = context.Request.Cookies[AdministratorBrowser.BudgetCookie];
        budget = CeremonySecrets.IsIdentifier(budget) ? budget : CeremonySecrets.Create();
        var correlation = CeremonySecrets.Create();
        var password = form[AdministratorBrowser.Password][0]!;
        var result = await passwords.BeginAsync(form[AdministratorBrowser.Username][0]!, password, AdministratorPolicy.Login,
            budget!, correlation, context.RequestAborted, continuation: entry.Request);
        if (result.Interaction is null)
        {
            if (result.NeedsDummyWork)
            {
                dummy.Verify(password);
                await denial.WriteAsync(context.RequestAborted);
            }
            return null;
        }
        RequireCurrent(context, entry);
        AdministratorBrowser.Bind(context, result.Interaction, budget!, correlation);
        return QueryHelpers.AddQueryString(BrowserAccountProfile.MfaPath, new Dictionary<string, string?>
        {
            [BrowserAccountProfile.Intent] = entry.Intent,
            [AdministratorBrowser.Interaction] = result.Interaction
        });
    }

    /// <summary>Checks browser/intent before consuming OTP, then requires the committed private completion grant before any cookie or redirect.</summary>
    internal async Task<string?> CompleteAsync(HttpContext context)
    {
        var form = await BrowserAccountRequest.FormAsync(context, profile, BrowserAccountProfile.Intent, AdministratorBrowser.Interaction, AdministratorBrowser.Code);
        if (form is null || !CeremonySecrets.IsIdentifier(form[AdministratorBrowser.Interaction][0]) ||
            proofs.Read(context, form[BrowserAccountProfile.Intent][0]!) is not { } entry)
        {
            return null;
        }
        var interaction = form[AdministratorBrowser.Interaction][0]!;
        var code = form[AdministratorBrowser.Code][0]!;
        if (code.Length != AdministratorPolicy.CodeDigits || code.Any(character => character is < '0' or > '9'))
        {
            return null;
        }
        var grant = await completion.CompleteBrowserAsync(interaction, AdministratorBrowser.Correlation(context, interaction), code,
            entry.Request!, context.RequestAborted);
        if (grant?.BrowserCompletion is not { } selector || IssuerContinuationPolicy.Digest(selector) is null)
        {
            return null;
        }
        RequireCurrent(context, entry);
        await tickets.IssueAsync(context, grant);
        RequireCurrent(context, entry);
        return QueryHelpers.AddQueryString(profile.Resume.AbsoluteUri, BrowserAccountProfile.Completion, selector);
    }

    /// <summary>Withholds all new response cookies on expiry/cancellation after an awaited boundary; the outer account guard returns a safe failure.</summary>
    private void RequireCurrent(HttpContext context, BrowserEntry entry)
    {
        if (context.RequestAborted.IsCancellationRequested || !entry.Current(clock))
        {
            context.Response.Headers.Remove(HeaderNames.SetCookie);
            throw new FactorUnavailableException();
        }
    }
}
