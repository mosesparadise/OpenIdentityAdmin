namespace IdentityPlatform.Testing;

public static class SafeAssertions
{
    public static void ContainsNoSensitiveMaterial(string content, params string[] sensitiveValues)
    {
        var containsSensitiveMaterial = sensitiveValues.Any(value => content.Contains(value, StringComparison.Ordinal));
        Assert.False(containsSensitiveMaterial, "Sensitive material was found in captured content.");
    }
}
