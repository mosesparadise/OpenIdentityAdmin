using System.Net;
using System.Text.RegularExpressions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Checks final native form policies independently, without printing page content, nonces or transport values.</summary>
internal static class BrowserFormHeaders
{
    private const string FormPolicy = "strict-origin";
    private const string InertPolicy = "no-referrer";
    private const string SourceSeparator = " ";
    private const string ScriptNonce = "<script\\b[^>]*\\bnonce=\"([^\"]+)\"";
    private const int ScriptCount = 2;
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string ContentSecurityPolicy = "Content-Security-Policy";
    private const string Prefix = "default-src 'none'; script-src 'nonce-";
    private const string Suffix = "'; style-src 'self'; img-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'self'";
    private const string LogoutPolicy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self' ";
    internal const string PresentationPolicy = "; style-src 'sha256-4HaK2EqdgpJzeoK+/uO11LS6Ub+KAOAmt9OnGAxAWuo='; script-src 'sha256-w+uuhquW+ZQV55tUvccOSS+TKS9Hbuz6bhtay55K5ho='";
    private const int NonceBytes = 24;

    /// <summary>Distinguishes actionable HTML documents from redirects, results and inert denials.</summary>
    internal static void Referrer(HttpResponseMessage response, bool form = false) =>
        Assert.True(Header(response, ReferrerPolicy) == (form ? FormPolicy : InertPolicy));

    /// <summary>Requires all original CSP directives, the actual rendered nonce and only the fixed MFA return origin.</summary>
    internal static async Task AccountAsync(HttpResponseMessage response, bool mfa = false)
    {
        Referrer(response, form: true);
        var policy = Header(response, ContentSecurityPolicy);
        var hasExpectedPrefix = policy.StartsWith(Prefix, StringComparison.Ordinal);
        Assert.True(hasExpectedPrefix);
        var end = policy.IndexOf(Suffix, StringComparison.Ordinal);
        Assert.True(end > Prefix.Length);
        var nonce = policy[Prefix.Length..end];
        Assert.True(Convert.TryFromBase64String(nonce, new byte[NonceBytes], out var used) && used == NonceBytes);
        var expected = Prefix + nonce + Suffix + (mfa ? SourceSeparator + new Uri(BrowserIssuerHost.Client).GetLeftPart(UriPartial.Authority) : string.Empty);
        Assert.True(policy == expected);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var scripts = Regex.Matches(html, ScriptNonce, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var count = scripts.Count;
        Assert.Equal(ScriptCount, count);
        Assert.True(scripts.All(match => WebUtility.HtmlDecode(match.Groups[1].Value) == nonce));
    }

    /// <summary>Requires only the configured BFF authority in the real native issuer confirmation's form policy.</summary>
    internal static void Logout(HttpResponseMessage response, Uri client)
    {
        Referrer(response, form: true);
        var policy = Header(response, ContentSecurityPolicy);
        Assert.True(policy == LogoutPolicy + client.GetLeftPart(UriPartial.Authority) + PresentationPolicy);
    }

    /// <summary>Checks finite cardinality before indexing so a duplicate header cannot disclose its raw values in diagnostics.</summary>
    private static string Header(HttpResponseMessage response, string name)
    {
        var found = response.Headers.TryGetValues(name, out var values);
        Assert.True(found);
        var headers = values!.ToArray();
        var count = headers.Length;
        Assert.Equal(1, count);
        return headers[0];
    }

}
