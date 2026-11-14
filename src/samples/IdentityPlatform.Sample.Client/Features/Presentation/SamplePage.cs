using System.Text.Encodings.Web;
using IdentityPlatform.DesignTokens;
using IdentityPlatform.Sample.Client.Features.ResourceAccess;
using IdentityPlatform.Sample.Client.Features.Sessions;
using Microsoft.AspNetCore.Antiforgery;

namespace IdentityPlatform.Sample.Client.Features.Presentation;

/// <summary>Renders a finite accessible local integration journey using the platform's shared light/dark assets.</summary>
internal static class SamplePage
{
    private const string NoStorePolicy = "no-store";
    private const string ReferrerPolicyHeader = "Referrer-Policy";
    private const string NoReferrerPolicy = "no-referrer";
    private const string ContentTypeOptionsHeader = "X-Content-Type-Options";
    private const string NoSniffPolicy = "nosniff";
    private const string ContentSecurityPolicy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self' https://issuer.dev.localhost:17443; object-src 'none'";
    private const string IntroductionMarkup = "<p>A disposable client for testing sign-in, consent, current profile and protected API access.</p>";
    private const string CatalogueSetupMarkup = "<h2>Configure this sample</h2><p>Create audience <code>orders-api</code>, API scope <code>orders.read</code> and identity scope <code>sample.email</code> with email disclosure in the administrator UI.</p>";
    private const string SetupCallbackPrefixMarkup = "<p>Create a managed confidential application with callback <code>";
    private const string ProtectedSetupMarkup = "</code> and privately retain its one-time secret before leaving the creation page. Explicitly assign both scopes. In the authenticated Aspire dashboard, run <strong>Configure sample client</strong> on sample-client and enter its client ID and retained secret. If the secret was lost, use Replace secret in the administrator UI and retain the replacement before continuing.</p>";
    private const string ReadyMarkup = "<p>Ready to sign in. Each sign-in requires a new password and MFA ceremony and explicit consent.</p>";
    private const string IdentityPrefixMarkup = "<p>Signed in as <strong>";
    private const string IdentitySuffixMarkup = "</strong>.</p>";
    private const string RequestedAuthorityMarkup = "<p>Requested scopes: <code>openid orders.read sample.email</code>. Expected API audience: <code>orders-api</code>.</p>";
    private const string LoginPath = "/login";
    private const string SignInLabel = "Sign in";
    private const string ProfilePath = "/profile";
    private const string ProfileLabel = "Fetch current profile";
    private const string OrdersPath = "/orders";
    private const string OrdersLabel = "Call orders API";
    private const string LogoutPath = "/logout";
    private const string LogoutLabel = "Sign out locally";
    private const string AuthorityLimitsMarkup = "<p>The orders API validates signed tokens offline: existing tokens can remain accepted until expiry. UserInfo checks current issuer authority. Local sign-out ends only this sample browser session.</p>";
    private const string HtmlContentType = "text/html; charset=utf-8";
    private const string PageTitle = "Sample application";
    private const string FormPrefixMarkup = "<form method=\"post\" action=\"";
    private const string HiddenFieldPrefixMarkup = "\"><input type=\"hidden\" name=\"";
    private const string FormValueMarkup = "\" value=\"";
    private const string ButtonPrefixMarkup = "\"><button type=\"submit\">";
    private const string FormSuffixMarkup = "</button></form>";
    private const string ResultPrefixMarkup = "<section aria-label=\"Result\"><p role=\"status\">";
    private const string ParagraphEndMarkup = "</p>";
    private const string SubjectPrefixMarkup = "<dl class=\"sample-profile\"><dt>Subject</dt><dd>";
    private const string DefinitionEndMarkup = "</dd>";
    private const string EmailPrefixMarkup = "<dt>Email</dt><dd>";
    private const string VerificationPrefixMarkup = "</dd><dt>Email verified</dt><dd>";
    private const string YesText = "Yes";
    private const string NoText = "No";
    private const string DefinitionListEndMarkup = "</dl>";
    private const string SectionEndMarkup = "</section>";

    /// <summary>Sets strict response policy before any sample response, including native challenge/callback redirects.</summary>
    internal static void Headers(HttpContext context)
    {
        context.Response.Headers.CacheControl = NoStorePolicy;
        context.Response.Headers[ReferrerPolicyHeader] = NoReferrerPolicy;
        context.Response.Headers[ContentTypeOptionsHeader] = NoSniffPolicy;
        context.Response.Headers.ContentSecurityPolicy = ContentSecurityPolicy + BrowserDocument.AssetPolicy;
    }

    /// <summary>Writes only encoded bounded identity projections; forms contain native antiforgery, never OAuth credentials.</summary>
    internal static async Task WriteAsync(HttpContext context, bool configured, string? subject = null, ResourceResult? result = null)
    {
        Headers(context);
        var body = IntroductionMarkup;
        if (!configured)
        {
            body += CatalogueSetupMarkup +
                SetupCallbackPrefixMarkup + SampleProfile.Callback + ProtectedSetupMarkup;
        }
        else
        {
            var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
            body += subject is null ? ReadyMarkup
                : IdentityPrefixMarkup + Encode(subject) + IdentitySuffixMarkup;
            body += RequestedAuthorityMarkup;
            body += Form(LoginPath, SignInLabel, tokens);
            if (subject is not null)
            {
                body += Form(ProfilePath, ProfileLabel, tokens) + Form(OrdersPath, OrdersLabel, tokens);
            }
            body += Form(LogoutPath, LogoutLabel, tokens);
        }
        body += Result(result) + AuthorityLimitsMarkup;
        context.Response.ContentType = HtmlContentType;
        await RequestBudget.AwaitAsync(context.Response.WriteAsync(BrowserDocument.Render(PageTitle, body), context.RequestAborted), context.RequestAborted);
        await RequestBudget.AwaitAsync(context.Response.StartAsync(context.RequestAborted), context.RequestAborted);
        await RequestBudget.AwaitAsync(context.Response.CompleteAsync(), context.RequestAborted);
    }

    /// <summary>Renders finite recoverable service failure without resolving authentication or reflecting remote exception details.</summary>
    internal static async Task WriteFailureAsync(HttpContext context)
    {
        const string title = "Sample unavailable";
        const string recovery = "<p>The operation could not be completed. Return to the sample home page to check setup or start a fresh sign-in. No automatic retry was performed.</p><p><a href=\"/\">Return to sample home</a></p>";
        Headers(context);
        context.Response.ContentType = HtmlContentType;
        await RequestBudget.AwaitAsync(context.Response.WriteAsync(BrowserDocument.Render(title, recovery), context.RequestAborted), context.RequestAborted);
        await RequestBudget.AwaitAsync(context.Response.StartAsync(context.RequestAborted), context.RequestAborted);
        await RequestBudget.AwaitAsync(context.Response.CompleteAsync(), context.RequestAborted);
    }

    /// <summary>Renders one fixed same-origin action with an encoded native request-verification token.</summary>
    private static string Form(string action, string label, AntiforgeryTokenSet tokens) =>
        FormPrefixMarkup + action + HiddenFieldPrefixMarkup + Encode(tokens.FormFieldName) + FormValueMarkup + Encode(tokens.RequestToken!) + ButtonPrefixMarkup + label + FormSuffixMarkup;

    /// <summary>Shows only the current operation's result; a denial cannot leave an earlier profile on the page.</summary>
    private static string Result(ResourceResult? result)
    {
        if (result is null)
        {
            return string.Empty;
        }
        var body = ResultPrefixMarkup + Encode(result.Message) + ParagraphEndMarkup;
        if (result.Subject is not null)
        {
            body += SubjectPrefixMarkup + Encode(result.Subject) + DefinitionEndMarkup;
            if (result.Email is not null)
            {
                body += EmailPrefixMarkup + Encode(result.Email) + VerificationPrefixMarkup + (result.EmailVerified == true ? YesText : NoText) + DefinitionEndMarkup;
            }
            body += DefinitionListEndMarkup;
        }
        return body + SectionEndMarkup;
    }

    /// <summary>Encodes all dynamic text and attribute values before embedding them in trusted markup.</summary>
    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
