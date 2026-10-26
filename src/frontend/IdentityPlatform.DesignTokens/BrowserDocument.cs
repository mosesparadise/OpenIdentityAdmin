using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace IdentityPlatform.DesignTokens;

/// <summary>Shares fixed presentation assets across the BFF and issuer; each caller retains its own response and form-action policy.</summary>
internal static class BrowserDocument
{
    private const string StylePolicy = "; style-src ";
    private const string ScriptPolicy = "; script-src ";
    private const string WindowsNewline = "\r\n";
    private const string Newline = "\n";
    private const string HashPrefix = "'sha256-";
    private const string HashSuffix = "'";
    private const string ResourceSeparator = ".";
    private const string MissingAsset = "A required browser presentation asset is unavailable.";
    private const string Tokens = "tokens.css";
    private const string Layout = "browser-document.css";
    private const string Theme = "theme.js";
    private const string Header = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>";
    private const string StyleStart = "</title><style>";
    private const string ScriptStart = "</style><script>";
    private const string BodyStart = "</script></head><body><a class=\"skip-link\" href=\"#main\">Skip to content</a><header><span class=\"wordmark\">OpenIdentityAdmin</span><label id=\"theme-control\" hidden>Appearance <select id=\"theme\"><option value=\"system\">System</option><option value=\"light\">Light</option><option value=\"dark\">Dark</option></select></label></header><main id=\"main\" tabindex=\"-1\"><section class=\"card\" aria-labelledby=\"heading\"><h1 id=\"heading\">";
    private const string HeadingEnd = "</h1>";
    private const string Footer = "</section></main></body></html>";
    private static readonly string Css = Read(Tokens) + "\n" + Read(Layout);
    private static readonly string Script = Read(Theme);
    internal static string AssetPolicy { get; } = StylePolicy + Hash(Css) + ScriptPolicy + Hash(Script);

    /// <summary>Encodes the title and wraps only caller-owned, already encoded body markup; request data must never be supplied as markup.</summary>
    internal static string Render(string title, string trustedBody)
    {
        var encoded = HtmlEncoder.Default.Encode(title);
        return Header + encoded + StyleStart + Css + ScriptStart + Script + BodyStart + encoded + HeadingEnd + trustedBody + Footer;
    }

    /// <summary>Reads a fixed embedded asset from the consuming assembly, normalizing line endings before both rendering and hashing.</summary>
    private static string Read(string name)
    {
        var assembly = typeof(BrowserDocument).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(item => item.EndsWith(ResourceSeparator + name, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(MissingAsset);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd().Replace(WindowsNewline, Newline, StringComparison.Ordinal);
    }

    /// <summary>Authorizes exactly the immutable UTF-8 bytes rendered inside one inline asset element, never arbitrary inline content.</summary>
    private static string Hash(string content) => HashPrefix + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(content))) + HashSuffix;
}
