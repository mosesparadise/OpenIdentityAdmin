using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace IdentityPlatform.Admin.Bff.Features.Documentation;

/// <summary>Reads pinned embedded assets and inserts only encoded presentation text into the fixed Swagger template.</summary>
internal static class DocumentationAssets
{
    internal const string ScalarBootstrap = "scalar-bootstrap.js";
    internal const string SwaggerBootstrap = "swagger.js";
    internal const string Theme = "theme.css";
    private const string Tokens = "tokens.css";
    private const string Index = "index.html";
    private const string ResourcePrefix = "IdentityPlatform.Admin.Bff.Features.Documentation.Assets.";
    private const string PlaceholderPattern = @"\{\{(PageTitle|BrowserDocumentLabel|AdminDocumentLabel)\}\}";
    private const string TitleKey = "PageTitle";
    private const string BrowserKey = "BrowserDocumentLabel";
    private const string Percent = "%";
    private const string EncodedPercent = "&#37;";
    private const string OpenBrace = "{";
    private const string CloseBrace = "}";
    private const string EncodedOpenBrace = "&#123;";
    private const string EncodedCloseBrace = "&#125;";
    private static readonly Regex Placeholders = new(PlaceholderPattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Read a code-owned embedded asset name; callers never accept asset paths from configuration or requests.</summary>
    internal static string Read(string name)
    {
        using var stream = typeof(DocumentationAssets).Assembly.GetManifestResourceStream(ResourcePrefix + name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Compose the shared theme tokens with the fixed viewer stylesheet.</summary>
    internal static string ReadTheme() => Read(Tokens) + Read(Theme);

    /// <summary>Encode the Scalar title for HTML and prevent the vendor's document-name substitution from interpreting text.</summary>
    internal static string ScalarTitle(string title) => HtmlEncoder.Default.Encode(title)
        .Replace(OpenBrace, EncodedOpenBrace, StringComparison.Ordinal).Replace(CloseBrace, EncodedCloseBrace, StringComparison.Ordinal);

    /// <summary>Return a fresh template stream; single-pass replacement prevents nested placeholders and later Swagger substitution.</summary>
    internal static Stream RenderSwagger(DocumentationOptions options)
    {
        var html = Placeholders.Replace(Read(Index), match => EncodeTemplateText(match.Groups[1].Value switch
        {
            TitleKey => options.PageTitle,
            BrowserKey => options.BrowserDocumentLabel,
            _ => options.AdminDocumentLabel
        }));
        return new MemoryStream(Encoding.UTF8.GetBytes(html));
    }

    /// <summary>Encode HTML plus the vendor placeholder introducer before Swagger performs its own template pass.</summary>
    private static string EncodeTemplateText(string text) => HtmlEncoder.Default.Encode(text).Replace(Percent, EncodedPercent, StringComparison.Ordinal);
}
