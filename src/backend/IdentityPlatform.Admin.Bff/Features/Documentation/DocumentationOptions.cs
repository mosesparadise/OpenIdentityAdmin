namespace IdentityPlatform.Admin.Bff.Features.Documentation;

/// <summary>Holds bounded viewer text without any route, authentication or asset activation setting.</summary>
internal sealed class DocumentationOptions
{
    internal const string SectionName = "Documentation:Viewer";
    private const int TextLimit = 120;
    public string PageTitle { get; set; } = string.Empty;
    public string BrowserDocumentLabel { get; set; } = string.Empty;
    public string AdminDocumentLabel { get; set; } = string.Empty;

    /// <summary>Reject absent, oversized or control-bearing labels without exposing their contents in diagnostics.</summary>
    internal bool IsValid() => IsText(PageTitle) && IsText(BrowserDocumentLabel) && IsText(AdminDocumentLabel);

    /// <summary>Accept ordinary bounded presentation text; renderers own context-sensitive encoding.</summary>
    private static bool IsText(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= TextLimit && !text.Any(char.IsControl);
}
