using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications;

/// <summary>Allows only exact same-origin managed forms and a single protected GET handle, independently of the native antiforgery filter.</summary>
internal static class ManagedAccountRequest
{
    internal const string Decision = "Decision";
    internal const string Approve = "approve";
    internal const string Deny = "deny";
    private const string FormMedia = "application/x-www-form-urlencoded";
    private const string FormUtf8 = "application/x-www-form-urlencoded; charset=utf-8";
    private const string Origin = "Origin";
    private const string Authorization = "Authorization";

    /// <summary>Rejects route aliases, unsafe transport and unexpected query fields before accessing protected data.</summary>
    internal static string? Get(HttpContext http)
    {
        if (!Transport(http) || !HttpMethods.IsGet(http.Request.Method) || http.Request.Query.Count != 1 ||
            http.Request.Query.Keys.Single() != ManagedFormProof.Field || http.Request.Query[ManagedFormProof.Field].Count != 1)
        {
            return null;
        }
        return http.Request.Query[ManagedFormProof.Field][0];
    }

    /// <summary>Reads only the current phase's finite scalar field set; body/form limits and native Razor antiforgery remain independent.</summary>
    internal static async Task<(IFormCollection Form, ManagedHumanRequest Request)?> PostAsync(HttpContext http)
    {
        var issuer = http.RequestServices.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue.Issuer;
        if (!Transport(http) || !HttpMethods.IsPost(http.Request.Method) || http.Request.Query.Count != 0 ||
            http.Request.Headers[Origin] is not { Count: 1 } origin || origin[0] != issuer?.GetLeftPart(UriPartial.Authority) ||
            http.Request.ContentType is not (FormMedia or FormUtf8))
        {
            return null;
        }
        var form = await http.Request.ReadFormAsync(http.RequestAborted);
        if (form[ManagedFormProof.Field].Count != 1 || ManagedFormProof.Read(http, form[ManagedFormProof.Field][0]) is not { } request)
        {
            return null;
        }
        var fields = request.State switch
        {
            ManagedLoginState.PendingPassword => new[] { ManagedFormProof.Field, AdministratorBrowser.Username, AdministratorBrowser.Password },
            ManagedLoginState.PendingMfa => [ManagedFormProof.Field, AdministratorBrowser.Code],
            ManagedLoginState.PendingConsent => [ManagedFormProof.Field, Decision],
            _ => []
        };
        var exact = fields.Length == 0 ? null : await AdministratorBrowser.FormAsync(http, fields);
        return exact is null ? null : (exact, request);
    }

    /// <summary>Requires the literal route and original raw target, forbidding authorization headers and encoded path aliases.</summary>
    private static bool Transport(HttpContext http) => http.Request.IsHttps && !http.Request.Headers.ContainsKey(Authorization) &&
        http.Request.Path == ManagedFormProof.Path && http.Request.PathBase == PathString.Empty &&
        http.Features.Get<IHttpRequestFeature>()?.RawTarget == ManagedFormProof.Path + http.Request.QueryString;
}
