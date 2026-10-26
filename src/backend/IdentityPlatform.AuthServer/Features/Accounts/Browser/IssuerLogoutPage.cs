using System.Text.Encodings.Web;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.DesignTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Accounts.Browser;

/// <summary>Renders bounded inert confirmation and finite local outcomes without exposing OAuth credentials.</summary>
internal static class IssuerLogoutPage
{
    private const string SourceSeparator = " ";
    private const string ConfirmationTitle = "Confirm issuer sign-out";
    private const string IncompleteTitle = "Issuer sign-out not confirmed";
    private const string IncompleteBody = "<p>Return to the administrator application. No issuer session was signed out by this request.</p>";
    private const string FormPrefix = "<form method=\"post\" action=\"";
    private const string TagEnd = "\">";
    private const string FormSuffix = "<button type=\"submit\">Sign out this issuer session</button></form>";
    private const string CancelPath = "/bff/login";
    private const string CancelStart = "<a href=\"";
    private const string CancelEnd = "\">Return without issuer sign-out</a>";
    private const string FieldPrefix = "<input type=\"hidden\" name=\"";
    private const string FieldValue = "\" value=\"";
    private const string NoStore = "no-store";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string NoReferrer = "no-referrer";
    private const string FormReferrer = "strict-origin";
    private const string NoSniff = "nosniff";

    private const string ContentType = "text/html; charset=utf-8";
    private const string Policy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";

    /// <summary>Includes only the four validated protocol values, protected confirmation and native antiforgery field.</summary>
    internal static Task ConfirmAsync(HttpContext http, OpenIddictRequest request, string confirmation, string antiforgery, Uri configuredReturn) => WriteAsync(http,
        ConfirmationTitle, FormPrefix + IssuerLogoutInput.Path + TagEnd +
        Field(OpenIddictConstants.Parameters.ClientId, request.ClientId!) + Field(OpenIddictConstants.Parameters.PostLogoutRedirectUri, request.PostLogoutRedirectUri!) +
        Field(IssuerLogoutInput.HintName, IssuerLogoutInput.Hint(request)!) + Field(OpenIddictConstants.Parameters.State, request.State!) +
        Field(IssuerLogoutInput.Confirmation, confirmation) + Field(AdministratorBrowser.Antiforgery, antiforgery) +
        FormSuffix + CancelStart + HtmlEncoder.Default.Encode(new Uri(configuredReturn, CancelPath).AbsoluteUri) + CancelEnd,
        StatusCodes.Status200OK, configuredReturn);

    /// <summary>Never interprets missing or mismatched authority as a successful remote revocation.</summary>
    internal static Task IncompleteAsync(HttpContext http) => WriteAsync(http, IncompleteTitle,
        IncompleteBody, StatusCodes.Status400BadRequest);

    /// <summary>Encodes all hidden values independently and never inserts caller-provided markup.</summary>
    private static string Field(string name, string value) => FieldPrefix + HtmlEncoder.Default.Encode(name) +
        FieldValue + HtmlEncoder.Default.Encode(value) + TagEnd;

    /// <summary>Applies sensitive-page protections to successful confirmation and denial alike.</summary>
    private static Task WriteAsync(HttpContext http, string title, string content, int status, Uri? configuredReturn = null)
    {
        http.Response.StatusCode = status;
        http.Response.ContentType = ContentType;
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers[ReferrerPolicy] = configuredReturn is null ? NoReferrer : FormReferrer;
        http.Response.Headers.ContentSecurityPolicy = (configuredReturn is null ? Policy : Policy + SourceSeparator + configuredReturn.GetLeftPart(UriPartial.Authority)) + BrowserDocument.AssetPolicy;
        http.Response.Headers.XContentTypeOptions = NoSniff;
        return http.Response.WriteAsync(BrowserDocument.Render(title, content), http.RequestAborted);
    }
}
