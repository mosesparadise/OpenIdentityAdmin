using System.Text.Json.Nodes;
using IdentityPlatform.Api.Contracts.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using ApiMetadata = IdentityPlatform.Admin.Api.Features.ApiContracts.OpenApiMetadataOptions;
using ApiTransformer = IdentityPlatform.Admin.Api.Features.ApiContracts.MetadataTransformer;
using BffMetadata = IdentityPlatform.Admin.Bff.Features.ApiContracts.OpenApiMetadataOptions;
using BffTransformer = IdentityPlatform.Admin.Bff.Features.ApiContracts.MetadataTransformer;

namespace IdentityPlatform.Api.Contracts.Tests.Features.Configuration;

/// <summary>Exercises configuration through native document generation, startup and export-time option resolution.</summary>
public sealed class MetadataTests
{
    private const string MetadataPrefix = "ApiContracts:Metadata:";
    private const string TitleField = "Title";
    private const string DescriptionField = "Description";
    private const string ConfiguredTitle = "Configured <metadata>";
    private const string ConfiguredDescription = "Configured contract description";
    private const string ApiPath = "/openapi/v1.json";
    private const string BffPath = "/openapi/bff-v1.json";
    private const string InfoKey = "info";
    private const string TitleKey = "title";
    private const string DescriptionKey = "description";
    private const int MaximumTitle = 120;
    private const int MaximumDescription = 2000;

    /// <summary>Prove configured titles reach real HTTP documents and the final hostile fixture override is actually present.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ConfiguredMetadata_When_NativeDocumentRequested_Then_TitleAndHostileFixtureArePresent(bool browserSurface)
    {
        await using var host = await ContractHost.StartAsync(browserSurface, configuration: Values(ConfiguredTitle, ConfiguredDescription));
        var text = await host.Client.GetStringAsync(browserSurface ? BffPath : ApiPath, TestContext.Current.CancellationToken);
        var info = JsonNode.Parse(text)![InfoKey]!;
        Assert.Equal("Configured <metadata>", info[TitleKey]!.GetValue<string>());
        Assert.Equal("NON-SHIPPED tooling fixture. <img alt=\"\" src=\"data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7\" onload=window.fixtureInjected=true>", info[DescriptionKey]!.GetValue<string>());
    }

    /// <summary>Require each host's actual appsettings and preserve validated overrides even when no listener is started.</summary>
    [Theory]
    [InlineData(false, "IdentityPlatform Admin API")]
    [InlineData(true, "IdentityPlatform Admin BFF browser API")]
    public async Task Given_RealHostSettings_When_TransformerRunsWithoutStartup_Then_MetadataIsConfigured(bool browserSurface, string expectedTitle)
    {
        var document = await GenerateWithoutStartupAsync(browserSurface);
        Assert.Equal(expectedTitle, document.Info!.Title);
        Assert.Equal("Contract foundation. No production management operations are implemented.", document.Info.Description);
        Assert.Equal("1", document.Info.Version);
        Assert.Equal("/", Assert.Single(document.Servers!).Url);
        document = await GenerateWithoutStartupAsync(browserSurface, Values(ConfiguredTitle, ConfiguredDescription));
        Assert.Equal("Configured <metadata>", document.Info!.Title);
        Assert.Equal("Configured contract description", document.Info.Description);
    }

    /// <summary>Reject missing, whitespace and control-bearing metadata during real startup without revealing input values.</summary>
    [Theory]
    [InlineData(false, "Title", null)]
    [InlineData(true, "Title", null)]
    [InlineData(false, "Description", " ")]
    [InlineData(true, "Description", " ")]
    [InlineData(false, "Title", "unsafe\ntitle")]
    [InlineData(true, "Description", "unsafe\ndescription")]
    public async Task Given_InvalidMetadata_When_HostStarts_Then_ValidationRejects(bool browserSurface, string field, string? value)
    {
        var configuration = new Dictionary<string, string?> { [MetadataPrefix + field] = value };
        await Assert.ThrowsAsync<OptionsValidationException>(async () =>
        {
            await using var host = await ContractHost.StartAsync(browserSurface, configuration: configuration);
        });
    }

    /// <summary>Check inclusive bounds and reject both fields beyond their limits during build-time generation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_MetadataLengthBoundaries_When_ExportResolvesOptions_Then_OnlyBoundedValuesPass(bool browserSurface)
    {
        var title = new string('T', MaximumTitle);
        var description = new string('D', MaximumDescription);
        var document = await GenerateWithoutStartupAsync(browserSurface, Values(title, description));
        Assert.Equal(title, document.Info!.Title);
        Assert.Equal(description, document.Info.Description);
        await Assert.ThrowsAsync<OptionsValidationException>(() => GenerateWithoutStartupAsync(browserSurface, Values(title + 'T', description)));
        await Assert.ThrowsAsync<OptionsValidationException>(() => GenerateWithoutStartupAsync(browserSurface, Values(title, description + 'D')));
        await Assert.ThrowsAsync<OptionsValidationException>(() => GenerateWithoutStartupAsync(browserSurface, Values(null, description)));
    }

    /// <summary>Prevent misspelled presentation fields from being silently ignored.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_UnknownMetadataField_When_ExportResolvesOptions_Then_BindingRejects(bool browserSurface)
    {
        const string UnknownField = "ApiContracts:Metadata:Typo";
        const string SyntheticValue = "synthetic";
        var configuration = new Dictionary<string, string?> { [UnknownField] = SyntheticValue };
        await Assert.ThrowsAsync<InvalidOperationException>(() => GenerateWithoutStartupAsync(browserSurface, configuration));
    }

    /// <summary>Bind the real host configuration and invoke its native DI transformer without starting a listener.</summary>
    private static async Task<OpenApiDocument> GenerateWithoutStartupAsync(bool browserSurface, IReadOnlyDictionary<string, string?>? configuration = null)
    {
        var builder = ContractFixtureConfiguration.CreateBuilder(browserSurface, false, configuration);
        await using var app = builder.Build();
        var document = new OpenApiDocument();
        if (browserSurface)
        {
            var transformer = new BffTransformer(app.Services.GetRequiredService<IOptions<BffMetadata>>());
            await transformer.TransformAsync(document, null!, TestContext.Current.CancellationToken);
        }
        else
        {
            var transformer = new ApiTransformer(app.Services.GetRequiredService<IOptions<ApiMetadata>>());
            await transformer.TransformAsync(document, null!, TestContext.Current.CancellationToken);
        }
        return document;
    }

    /// <summary>Build independently authored presentation overrides, without reading production option limits.</summary>
    private static Dictionary<string, string?> Values(string? title, string description) => new()
    {
        [MetadataPrefix + TitleField] = title,
        [MetadataPrefix + DescriptionField] = description
    };
}
