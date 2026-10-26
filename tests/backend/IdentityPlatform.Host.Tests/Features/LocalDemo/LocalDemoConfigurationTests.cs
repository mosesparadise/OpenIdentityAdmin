using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Protection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Host.Tests.Features.LocalDemo;

/// <summary>Proves activation is explicit and malformed configuration never silently becomes a valid demo.</summary>
public sealed class LocalDemoConfigurationTests
{
    private const string Development = "Development";
    private const string EnabledSetting = "LocalDemo:Enabled";
    private const string Disabled = "false";
    private const string SensitiveSentinel = "synthetic-sensitive-sentinel";
    /// <summary>Only explicit true in Development activates the profile.</summary>
    [Theory]
    [InlineData(null, "Production", false)]
    [InlineData(Disabled, "Production", false)]
    [InlineData("FALSE", Development, false)]
    [InlineData("true", Development, true)]
    [InlineData("TRUE", "development", true)]
    public void Given_ExplicitActivationValue_When_Checked_Then_EnvironmentAndOptInAreBothRequired(string? value, string environment, bool expected) =>
        Assert.Equal(expected, LocalDemoProfile.IsEnabled(value, environment));

    /// <summary>Malformed and non-development activation values fail with a fixed diagnostic.</summary>
    [Theory]
    [InlineData("true", "Production")]
    [InlineData("true", "Staging")]
    [InlineData(" true", Development)]
    [InlineData("1", Development)]
    [InlineData("", Development)]
    public void Given_InvalidActivation_When_Checked_Then_FiniteConfigurationFailureIsReturned(string value, string environment) =>
        Assert.Equal(LocalDemoProfile.Failure, Assert.Throws<LocalDemoConfigurationException>(() => LocalDemoProfile.IsEnabled(value, environment)).Message);

    /// <summary>Unknown, nested and scalar profile shapes reject before material staging, even when Enabled is false.</summary>
    [Theory]
    [InlineData("LocalDemo")]
    [InlineData("LocalDemo:Unexpected")]
    [InlineData("LocalDemo:Enabled:Nested")]
    [InlineData("LocalDemo:MaterialRoot:Nested")]
    public void Given_AmbiguousConfiguration_When_Checked_Then_UnknownFieldsCannotBeIgnored(string key)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [EnabledSetting] = Disabled, [key] = SensitiveSentinel });
        var error = Assert.Throws<LocalDemoConfigurationException>(() => LocalDemoConfiguration.IsEnabled(builder.Configuration, builder.Environment));
        Assert.Equal(LocalDemoProfile.Failure, error.Message);
    }
}
