using IdentityPlatform.Admin.Bff.Features.Documentation;
using IdentityPlatform.Api.Contracts.Tests.Fixtures;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Configuration;

/// <summary>Verifies viewer option validation and literal encoding before vendor template processing.</summary>
public sealed class ViewerConfigurationTests
{
    internal const string HostileTitle = "</title><script>window.settingsInjected=true</script>{documentName} %(ConfigObject)";
    internal const string BrowserLabel = "Browser <img src=x onerror=window.settingsInjected=true> {{PageTitle}} %(ConfigObject)";
    internal const string AdminLabel = "Downstream <admin> %(DocumentTitle)";
    internal const string TitleSetting = "Documentation:Viewer:PageTitle";
    internal const string BrowserSetting = "Documentation:Viewer:BrowserDocumentLabel";
    internal const string AdminSetting = "Documentation:Viewer:AdminDocumentLabel";
    private const string SwaggerPath = "/docs/swagger/index.html";
    private const int OversizedLength = 121;

    /// <summary>Ensure startup rejects absent viewer text rather than falling back to hardcoded labels.</summary>
    [Theory]
    [InlineData("Documentation:Viewer:PageTitle", null)]
    [InlineData("Documentation:Viewer:BrowserDocumentLabel", " ")]
    [InlineData("Documentation:Viewer:AdminDocumentLabel", "bad\nlabel")]
    public async Task Given_InvalidViewerText_When_HostStarts_Then_ValidationRejects(string field, string? value)
    {
        var configuration = new Dictionary<string, string?> { [field] = value };
        await Assert.ThrowsAsync<OptionsValidationException>(async () =>
        {
            await using var host = await ContractHost.StartAsync(true, configuration: configuration);
        });
    }

    /// <summary>Reject oversized text and unknown viewer options without introducing a configuration-based activation switch.</summary>
    [Fact]
    public async Task Given_OversizedOrUnknownViewerSetting_When_HostStarts_Then_ConfigurationRejects()
    {
        var configuration = new Dictionary<string, string?> { [TitleSetting] = new string('T', OversizedLength) };
        await Assert.ThrowsAsync<OptionsValidationException>(async () =>
        {
            await using var host = await ContractHost.StartAsync(true, configuration: configuration);
        });
        const string UnknownField = "Documentation:Viewer:Enabled";
        const string EnabledValue = "true";
        configuration = new Dictionary<string, string?> { [UnknownField] = EnabledValue };
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var host = await ContractHost.StartAsync(true, configuration: configuration);
        });
    }

    /// <summary>Exercise the served Swagger template twice to detect consumed streams, unsafe insertion and nested/vendor substitution.</summary>
    [Fact]
    public async Task Given_HostileViewerText_When_SwaggerTemplateRequestedTwice_Then_TextStaysEncodedAndStable()
    {
        await using var host = await ContractHost.StartAsync(true, configuration: HostileSettings());
        var first = await host.Client.GetStringAsync(SwaggerPath, TestContext.Current.CancellationToken);
        var second = await host.Client.GetStringAsync(SwaggerPath, TestContext.Current.CancellationToken);
        Assert.Equal(first, second);
        Assert.Contains("&lt;/title&gt;&lt;script&gt;window.settingsInjected=true&lt;/script&gt;{documentName} &#37;(ConfigObject)", first, StringComparison.Ordinal);
        Assert.Contains("{{PageTitle}} &#37;(ConfigObject)", first, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>window.settingsInjected", first, StringComparison.Ordinal);
    }

    /// <summary>Prove the Scalar HTML title is safe before its document-name interpolation.</summary>
    [Fact]
    public void Given_HostileTitle_When_ScalarTitleEncoded_Then_MarkupAndPlaceholderAreLiteral()
    {
        Assert.Equal("&lt;/title&gt;&lt;script&gt;window.settingsInjected=true&lt;/script&gt;&#123;documentName&#125; %(ConfigObject)", DocumentationAssets.ScalarTitle(HostileTitle));
    }

    /// <summary>Provide reusable synthetic attack input; expected rendering stays independently asserted in each regression.</summary>
    internal static Dictionary<string, string?> HostileSettings() => new()
    {
        [TitleSetting] = HostileTitle,
        [BrowserSetting] = BrowserLabel,
        [AdminSetting] = AdminLabel
    };
}
