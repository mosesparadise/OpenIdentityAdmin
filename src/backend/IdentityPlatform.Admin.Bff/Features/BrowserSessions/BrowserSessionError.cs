namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Provides a finite inert recovery view without reflecting protocol details or automatically starting a new ceremony.</summary>
internal static class BrowserSessionError
{
    internal const string Path = "/bff/login/error";
    private const string Html = "text/html; charset=utf-8";
    private const string Content = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Sign-in unavailable</title></head><body><main><h1>Sign-in unavailable</h1><p>This sign-in could not be completed. Start a new sign-in when you are ready.</p><a href=\"/bff/login\">Return to sign-in</a></main></body></html>";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string ContentSecurityPolicy = "Content-Security-Policy";
    private const string Policy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
    private const string NoStore = "no-store";
    private const string NoReferrer = "no-referrer";

    /// <summary>Accepts only the fixed bodyless query-free HTTPS page; untrusted error text never enters output.</summary>
    internal static async Task<IResult> ResultAsync(HttpContext http)
    {
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers[ReferrerPolicy] = NoReferrer;
        http.Response.Headers[ContentSecurityPolicy] = Policy;
        if (!BrowserLoginRequest.Get(http.Request) || http.Request.Query.Count != 0 || http.Request.ContentType is not null || !await BrowserLoginRequest.EmptyAsync(http))
        {
            return Results.BadRequest();
        }
        return Results.Content(Content, Html);
    }
}
