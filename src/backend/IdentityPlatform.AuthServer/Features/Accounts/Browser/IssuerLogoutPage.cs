using System.Text.Encodings.Web;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Accounts.Browser;

/// <summary>Renders bounded inert confirmation and finite local outcomes without exposing OAuth credentials.</summary>
internal static class IssuerLogoutPage
{
    private const string ConfirmationTitle = "Confirm issuer sign-out";
    private const string IncompleteTitle = "Issuer sign-out not confirmed";
    private const string IncompleteBody = "<p>Return to the administrator application. No issuer session was signed out by this request.</p>";
    private const string FormPrefix = "<form method=\"post\" action=\"";
    private const string TagEnd = "\">";
    private const string FormSuffix = "<button type=\"submit\">Sign out this issuer session</button></form>";
    private const string FieldPrefix = "<input type=\"hidden\" name=\"";
    private const string FieldValue = "\" value=\"";
    private const string NoStore = "no-store";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string NoReferrer = "no-referrer";
    private const string NoSniff = "nosniff";
    private const string Header = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>";
    private const string TitleEnd = "</title></head><body><h1>";
    private const string HeadingEnd = "</h1>";
    private const string Footer = "</body></html>";

    private const string ContentType = "text/html; charset=utf-8";
    private const string Policy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";

    /// <summary>Includes only the four validated protocol values, protected confirmation and native antiforgery field.</summary>
    internal static Task ConfirmAsync(HttpContext http, OpenIddictRequest request, string confirmation, string antiforgery) => WriteAsync(http,
        ConfirmationTitle, FormPrefix + IssuerLogoutInput.Path + TagEnd +
        Field(OpenIddictConstants.Parameters.ClientId, request.ClientId!) + Field(OpenIddictConstants.Parameters.PostLogoutRedirectUri, request.PostLogoutRedirectUri!) +
        Field(IssuerLogoutInput.HintName, IssuerLogoutInput.Hint(request)!) + Field(OpenIddictConstants.Parameters.State, request.State!) +
        Field(IssuerLogoutInput.Confirmation, confirmation) + Field(AdministratorBrowser.Antiforgery, antiforgery) +
        FormSuffix, StatusCodes.Status200OK);

    /// <summary>Never interprets missing or mismatched authority as a successful remote revocation.</summary>
    internal static Task IncompleteAsync(HttpContext http) => WriteAsync(http, IncompleteTitle,
        IncompleteBody, StatusCodes.Status400BadRequest);

    /// <summary>Encodes all hidden values independently and never inserts caller-provided markup.</summary>
    private static string Field(string name, string value) => FieldPrefix + HtmlEncoder.Default.Encode(name) +
        FieldValue + HtmlEncoder.Default.Encode(value) + TagEnd;

    /// <summary>Applies sensitive-page protections to successful confirmation and denial alike.</summary>
    private static Task WriteAsync(HttpContext http, string title, string content, int status)
    {
        http.Response.StatusCode = status;
        http.Response.ContentType = ContentType;
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers[ReferrerPolicy] = NoReferrer;
        http.Response.Headers.ContentSecurityPolicy = Policy;
        http.Response.Headers.XContentTypeOptions = NoSniff;
        return http.Response.WriteAsync(Header + title +
            TitleEnd + title + HeadingEnd + content + Footer, http.RequestAborted);
    }
}
