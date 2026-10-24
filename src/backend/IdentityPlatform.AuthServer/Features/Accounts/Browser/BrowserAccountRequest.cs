using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

namespace IdentityPlatform.AuthServer.Features.Accounts.Browser;

/// <summary>Keeps browser continuation forms separate from query-free standalone forms and requires the configured exact issuer origin.</summary>
internal static class BrowserAccountRequest
{
    private const string OriginHeader = "Origin";
    private const string AuthorizationHeader = "Authorization";
    private const string FormMedia = "application/x-www-form-urlencoded";
    private const string FormUtf8 = "application/x-www-form-urlencoded; charset=utf-8";

    /// <summary>Accepts only one canonical intent, plus the exact interaction locator on the dedicated MFA GET.</summary>
    internal static string? Intent(HttpContext context, bool mfa)
    {
        var query = context.Request.Query;
        var fields = mfa ? new[] { BrowserAccountProfile.Intent, AdministratorBrowser.Interaction } : [BrowserAccountProfile.Intent];
        return query.Count == fields.Length && query.Keys.All(key => fields.Contains(key, StringComparer.Ordinal)) &&
            fields.All(key => query[key].Count == 1) && IssuerContinuationPolicy.Digest(query[BrowserAccountProfile.Intent][0]) is not null &&
            (!mfa || CeremonySecrets.IsIdentifier(query[AdministratorBrowser.Interaction][0])) ? query[BrowserAccountProfile.Intent][0] : null;
    }

    /// <summary>Runs after native antiforgery and account body limits; callbacks do not share this explicit POST mutation guard.</summary>
    internal static Task<IFormCollection?> FormAsync(HttpContext context, BrowserAccountProfile profile, params string[] fields)
    {
        var request = context.Request;
        return request.IsHttps && HttpMethods.IsPost(request.Method) && !request.Headers.ContainsKey(AuthorizationHeader) &&
            request.Headers[OriginHeader] is { Count: 1 } origin && origin[0] == profile.Origin && request.ContentType is FormMedia or FormUtf8
            ? AdministratorBrowser.FormAsync(context, fields) : Task.FromResult<IFormCollection?>(null);
    }
}
