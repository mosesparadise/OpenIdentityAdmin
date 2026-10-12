namespace IdentityPlatform.Admin.Bff.Features.ApiContracts;

/// <summary>Contains required presentation metadata; protocol versions and security policy remain code-owned.</summary>
internal sealed class OpenApiMetadataOptions
{
    internal const string SectionName = "ApiContracts:Metadata";
    internal const int TitleLimit = 120;
    internal const int DescriptionLimit = 2000;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Reject missing, oversized or control-bearing metadata without returning configuration contents.</summary>
    internal bool IsValid() => IsText(Title, TitleLimit) && IsText(Description, DescriptionLimit);

    /// <summary>Allow ordinary Unicode and markup text within a bounded single-line value.</summary>
    private static bool IsText(string? value, int limit) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= limit && !value.Any(char.IsControl);
}
