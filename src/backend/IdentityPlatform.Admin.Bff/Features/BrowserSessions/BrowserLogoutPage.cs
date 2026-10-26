using System.Text.Encodings.Web;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Provides finite local and issuer-assisted navigation outcomes without claiming issuer revocation.</summary>
internal static class BrowserLogoutPage
{
    private const string LinkStart = "<a id=\"issuer-sign-out\" href=\"";
    private const string LinkEnd = "\">Continue to issuer sign-out confirmation</a>";
    internal const string Path = "/bff/logout";
    private const string Html = "text/html; charset=utf-8";
    private const string Policy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
    private const string Header = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Administrator sign-out</title></head><body><main><h1>Administrator sign-out</h1>";
    private const string Footer = "</main></body></html>";

    private const string FormContent = "<p>End this administrator session and cancel any sign-in in progress. Choose whether to continue to issuer confirmation after local sign-out.</p><form method=\"post\" action=\"/bff/logout\"><input type=\"hidden\" name=\"{0}\" value=\"{1}\"><button type=\"submit\" name=\"Mode\" value=\"local\">Sign out locally</button><button type=\"submit\" name=\"Mode\" value=\"issuer\">Sign out locally and continue to issuer</button></form>";
    private const string CommittedContent = "<p>This administrator session has ended locally. Issuer sign-out was not requested.</p><a href=\"/bff/login\">Sign in again</a>";
    private const string UnconfirmedContent = "<p>Local sign-out could not be confirmed. Issuer sign-out was not requested.</p><a href=\"/bff/logout\">Check local sign-out</a>";
    private const string IncompleteContent = "<p>This administrator session has ended locally. Issuer sign-out is incomplete or unverified. Refresh capability is not enabled.</p><a href=\"/bff/login\">Sign in again</a>";
    private const string ReturnedContent = "<p>This administrator session has ended locally. Issuer sign-out result is unverified. Refresh capability is not enabled.</p><a href=\"/bff/login\">Sign in again</a>";
    private const string ReturnUnavailableContent = "<p>Issuer sign-out result could not be checked. No session was created.</p><a href=\"/bff/login\">Sign in again</a>";
    private const string NoStore = "no-store";
    private const string NoReferrer = "no-referrer";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string ContentSecurityPolicy = "Content-Security-Policy";
    private static readonly System.Text.CompositeFormat FormFormat = System.Text.CompositeFormat.Parse(FormContent);

    /// <summary>Uses only a native protected token in a fixed action; rendering does not cancel pending work or authenticate the browser.</summary>
    internal static IResult Form(HttpContext http, string token) => Page(http,
        string.Format(System.Globalization.CultureInfo.InvariantCulture, FormFormat, BrowserRequestPolicy.AntiforgeryField, HtmlEncoder.Default.Encode(token)), StatusCodes.Status200OK);

    /// <summary>Reports only durable local invalidation after all owned cleanup and deadline checks have succeeded.</summary>
    internal static IResult Committed(HttpContext http) => Page(http, CommittedContent, StatusCodes.Status200OK);

    /// <summary>Preserves known local success after remote quota, cancellation, preparation or cleanup failure.</summary>
    internal static IResult Incomplete(HttpContext http) => Page(http, IncompleteContent, StatusCodes.Status200OK);

    /// <summary>A correlated browser return is deliberately not an attestation of issuer revocation.</summary>
    internal static IResult Returned(HttpContext http) => Page(http, ReturnedContent, StatusCodes.Status200OK);

    /// <summary>Describes a failed return without trusting unverified query or cookie content as local status.</summary>
    internal static IResult ReturnUnavailable(HttpContext http) => Page(http, ReturnUnavailableContent, StatusCodes.Status503ServiceUnavailable);

    /// <summary>Offers only the fixed issuer link after committed remote preparation; no automatic navigation or credential is emitted.</summary>
    internal static IResult Continue(HttpContext http, string link) => Page(http, IncompleteContent +
        LinkStart + HtmlEncoder.Default.Encode(link) + LinkEnd, StatusCodes.Status200OK);

    /// <summary>Offers an explicit bounded check after uncertain completion; no automatic request, retry or rollback claim is made.</summary>
    internal static IResult Unconfirmed(HttpContext http) => Page(http, UnconfirmedContent, StatusCodes.Status503ServiceUnavailable);

    /// <summary>Applies the same fixed noncacheable and nonembeddable HTML policy to every rendered outcome.</summary>
    private static IResult Page(HttpContext http, string content, int status)
    {
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers[ReferrerPolicy] = NoReferrer;
        http.Response.Headers[ContentSecurityPolicy] = Policy;
        return Results.Content(Header + content + Footer, Html, statusCode: status);
    }
}
