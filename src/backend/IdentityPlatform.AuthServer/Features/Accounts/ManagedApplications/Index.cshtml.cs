using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications;

/// <summary>Renders separate managed-purpose password/MFA/explicit consent without consulting issuer authentication cookies.</summary>
/// <example><code>// Follow the issuer's native managed authorization cache redirect, then submit only its protected rendered form.</code></example>
public sealed class IndexModel : PageModel
{
    private const string Referrer = "Referrer-Policy";
    private const string FormReferrer = "strict-origin";
    public string? Flow
    {
        get; private set;
    }
    public string? ClientId
    {
        get; private set;
    }
    public string? Error
    {
        get; private set;
    }
    public int Phase
    {
        get; private set;
    }

    /// <summary>Reads current phase under native/account locks; GET cannot verify a factor or grant consent.</summary>
    /// <example><code>// GET the fixed managed sign-in path with the issuer's protected Flow handle.</code></example>
    public async Task<IActionResult> OnGetAsync()
    {
        Flow = ManagedAccountRequest.Get(HttpContext);
        var request = ManagedFormProof.Read(HttpContext, Flow);
        if (request is null)
        {
            return BadRequest();
        }
        var result = await ExecuteAsync(request, ManagedHumanAction.Read, null, null);
        ClientId = result.ClientId;
        Phase = (int)result.State;
        Response.Headers[Referrer] = FormReferrer;
        return Page();
    }

    /// <summary>Runs only the explicit current form phase with genuine native antiforgery and current durable managed binding.</summary>
    /// <example><code>// POST the rendered Flow, native CSRF token and only the phase-specific password, code or decision fields.</code></example>
    public async Task<IActionResult> OnPostAsync()
    {
        var input = await ManagedAccountRequest.PostAsync(HttpContext);
        if (input is not { } value)
        {
            return BadRequest();
        }
        var form = value.Form;
        var request = value.Request;
        var action = Action(request, form);
        if (action is null)
        {
            return BadRequest();
        }
        var result = await ExecuteAsync(request, action.Value, form[AdministratorBrowser.Username].SingleOrDefault(),
            action == ManagedHumanAction.Password ? form[AdministratorBrowser.Password].SingleOrDefault() : form[AdministratorBrowser.Code].SingleOrDefault());
        Flow = ManagedFormProof.Protect(HttpContext, ManagedFormProof.Advance(request, result));
        if (result.Accepted)
        {
            return LocalRedirect(QueryHelpers.AddQueryString(ManagedFormProof.Path, ManagedFormProof.Field, Flow));
        }
        ClientId = result.ClientId;
        Phase = (int)result.State;
        Error = AdministratorBrowser.Denied;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        Response.Headers[Referrer] = FormReferrer;
        return Page();
    }

    /// <summary>Rejects invalid scalar bounds and accepts only literal approve/deny decisions; no posted subject, scope or callback is trusted.</summary>
    private static ManagedHumanAction? Action(ManagedHumanRequest request, IFormCollection form) => request.State switch
    {
        ManagedLoginState.PendingPassword when form[AdministratorBrowser.Username][0]!.Length <= AdministratorBrowser.UsernameLength &&
            form[AdministratorBrowser.Password][0]!.Length <= AdministratorBrowser.PasswordLength => ManagedHumanAction.Password,
        ManagedLoginState.PendingMfa when form[AdministratorBrowser.Code][0] is { Length: AdministratorPolicy.CodeDigits } code &&
            code.All(character => character is >= '0' and <= '9') => ManagedHumanAction.Mfa,
        ManagedLoginState.PendingConsent when form[ManagedAccountRequest.Decision][0] == ManagedAccountRequest.Approve => ManagedHumanAction.Approve,
        ManagedLoginState.PendingConsent when form[ManagedAccountRequest.Decision][0] == ManagedAccountRequest.Deny => ManagedHumanAction.Deny,
        _ => null
    };

    /// <summary>Uses the original managed browser's separate shared-budget identity; records publication only after actual commit and transaction cleanup.</summary>
    private async Task<ManagedHumanResult> ExecuteAsync(ManagedHumanRequest request, ManagedHumanAction action, string? username, string? proof)
    {
        var browser = ManagedBrowser.Read(HttpContext) ?? throw ProtocolStorageOwner.Unavailable();
        var current = ProtocolStorageAmbient.Current ?? throw ProtocolStorageOwner.Unavailable();
        var result = await ManagedHumanOperation.RunAsync(current.Scope, request, action,
            WebEncoders.Base64UrlEncode(Convert.FromHexString(browser)), username, proof, HttpContext.RequestAborted);
        if (result.NeedsDummyWork)
        {
            HttpContext.RequestServices.GetRequiredService<DummyPassword>().Verify(proof!);
        }
        ManagedAccountBoundary.Completed(HttpContext);
        return result;
    }
}
