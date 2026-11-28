using IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.AuthServer.Features.Protocol.Devices;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Accounts.Devices;

/// <summary>Accepts the short human reference displayed by the device and opens a separate fresh managed account ceremony.</summary>
/// <example><code>// Open the issuer verification URI from the device, then enter the code on its screen.</code></example>
public sealed class IndexModel : PageModel
{
    internal const string Path = "/account/device";
    private const string Code = "UserCode";
    private const string Origin = "Origin";
    private const int MaximumCode = 32;
    public bool Failed
    {
        get; private set;
    }

    /// <summary>Displays the code-entry form for an exact same-origin HTTPS request.</summary>
    /// <example><code>// GET /account/device</code></example>
    public IActionResult OnGet()
    {
        if (!Transport())
        {
            return BadRequest();
        }
        if (ManagedBrowser.Read(HttpContext) is null)
        {
            _ = ManagedBrowser.GetOrCreate(HttpContext);
        }
        ManagedFormPolicy.DeviceEntry(HttpContext);
        return Page();
    }

    /// <summary>Resolves the native user code before creating a browser-bound password, MFA and consent request.</summary>
    /// <example><code>// Submit the native antiforgery value and the displayed UserCode to /account/device.</code></example>
    public async Task<IActionResult> OnPostAsync()
    {
        var issuer = HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue.Issuer;
        if (!Transport() || Request.Headers[Origin] is not { Count: 1 } origin || origin[0] != issuer?.GetLeftPart(UriPartial.Authority) ||
            await AdministratorBrowser.FormAsync(HttpContext, Code) is not { } form || form[Code][0] is not { Length: > 0 and <= MaximumCode } code ||
            !code.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or ' '))
        {
            return BadRequest();
        }
        var current = ProtocolStorageAmbient.Current ?? throw ProtocolStorageOwner.Unavailable();
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(current.Scope.Owner);
        var native = await DeviceNative.CreateAsync(current.Scope, OpenIddictServerEndpointType.EndUserVerification, new()
        {
            UserCode = code
        });
        var principal = await native.UserAsync(HttpContext.RequestAborted);
        var flow = principal is null ? null : await ManagedDeviceInteraction.StartAsync(current.Scope, principal, code, ManagedBrowser.GetOrCreate(HttpContext),
            native.Transaction.Options.TokenValidationParameters.ClockSkew, HttpContext.RequestAborted);
        await transaction.CommitAsync(HttpContext.RequestAborted);
        ManagedAccountBoundary.Completed(HttpContext);
        if (flow is null)
        {
            ManagedFormPolicy.DeviceEntry(HttpContext);
            Failed = true;
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Page();
        }
        current.Request.RestrictPublication(DateTimeOffset.FromUnixTimeSeconds(flow.ExpiresAt));
        return LocalRedirect(QueryHelpers.AddQueryString(ManagedFormProof.Path, ManagedFormProof.Field, ManagedFormProof.Protect(HttpContext, flow)));
    }

    /// <summary>Accepts only the original fixed route and keeps the human verification code out of URLs.</summary>
    private bool Transport() => Request.IsHttps && Request.PathBase == PathString.Empty && Request.Path == Path && !Request.QueryString.HasValue &&
        Request.Headers.Authorization.Count == 0 && HttpContext.Features.Get<IHttpRequestFeature>()?.RawTarget == Path;
}
