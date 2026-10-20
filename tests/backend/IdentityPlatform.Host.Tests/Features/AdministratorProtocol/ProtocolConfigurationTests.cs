using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Checks the independent protocol switch and exact immutable registration configuration without contacting a database.</summary>
public sealed class ProtocolConfigurationTests(AdministratorRegistrationFixture fixture) : IClassFixture<AdministratorRegistrationFixture>
{
    private const string Callback = "https://administrator.example.test/callback";
    private const string Enabled = "Identity:AdministratorProtocol:Enabled";
    private const string Redirect = "Identity:AdministratorProtocol:RedirectUri";
    private const string PublicKey = "Identity:AdministratorProtocol:PublicKey";
    private const string InsecureCallback = "http://administrator.example.test/callback";
    private const string UnknownField = "Identity:AdministratorProtocol:Unknown";
    private const string NestedField = "Identity:AdministratorProtocol:Enabled:Nested";
    private const string Invalid = "synthetic-private-setting";

    /// <summary>Account activation alone never enables protocol routes or requires registration input.</summary>
    [Fact]
    public void Given_AbsentOrFalseSwitch_When_ConfigurationIsRead_Then_ProtocolRemainsDisabled()
    {
        using var configuration = new ConfigurationManager();
        Assert.Null(AdministratorProtocolConfiguration.Read(configuration, true, null));
        configuration[Enabled] = bool.FalseString;
        Assert.Null(AdministratorProtocolConfiguration.Read(configuration, false, null));
    }

    /// <summary>Requires both existing account protection and valid explicit public registration data.</summary>
    [Theory]
    [InlineData(ProtocolConfigurationFault.None)]
    [InlineData(ProtocolConfigurationFault.AccountsDisabled)]
    [InlineData(ProtocolConfigurationFault.NoProtection)]
    [InlineData(ProtocolConfigurationFault.MissingCallback)]
    [InlineData(ProtocolConfigurationFault.InsecureCallback)]
    [InlineData(ProtocolConfigurationFault.MissingKey)]
    [InlineData(ProtocolConfigurationFault.InvalidKey)]
    [InlineData(ProtocolConfigurationFault.InvalidSwitch)]
    [InlineData(ProtocolConfigurationFault.UnknownField)]
    [InlineData(ProtocolConfigurationFault.NestedField)]
    [InlineData(ProtocolConfigurationFault.SectionValue)]
    public void Given_ExplicitConfiguration_When_Validated_Then_OnlyCompleteProtectedProfileIsAccepted(ProtocolConfigurationFault fault)
    {
        using var material = new IssuerMaterialFixture();
        using var credentials = new ServiceCollection().AddIssuerProtection(material.Configuration)!;
        using var configuration = new ConfigurationManager();
        configuration[Enabled] = bool.TrueString;
        configuration[Redirect] = Callback;
        configuration[PublicKey] = fixture.Json;
        Mutate(configuration, fault);
        if (fault == ProtocolConfigurationFault.None)
        {
            var policy = AdministratorProtocolConfiguration.Read(configuration, true, credentials);
            Assert.NotNull(policy);
            Assert.Equal(Callback, policy.RedirectUri);
            Assert.Equal(fixture.Key.Fingerprint, policy.Key.Fingerprint);
            return;
        }
        var error = Assert.Throws<AdministratorProtocolConfigurationException>(() => AdministratorProtocolConfiguration.Read(configuration,
            fault != ProtocolConfigurationFault.AccountsDisabled, fault == ProtocolConfigurationFault.NoProtection ? null : credentials));
        Assert.Equal("Invalid administrator protocol configuration. Explicit activation requires administrator accounts, issuer protection and the fixed public registration profile.", error.Message);
    }

    /// <summary>Changes one configuration boundary at a time without exposing submitted values in diagnostics.</summary>
    private static void Mutate(ConfigurationManager configuration, ProtocolConfigurationFault fault)
    {
        switch (fault)
        {
            case ProtocolConfigurationFault.MissingCallback:
                configuration[Redirect] = null;
                break;
            case ProtocolConfigurationFault.InsecureCallback:
                configuration[Redirect] = InsecureCallback;
                break;
            case ProtocolConfigurationFault.MissingKey:
                configuration[PublicKey] = null;
                break;
            case ProtocolConfigurationFault.InvalidKey:
                configuration[PublicKey] = Invalid;
                break;
            case ProtocolConfigurationFault.InvalidSwitch:
                configuration[Enabled] = Invalid;
                break;
            case ProtocolConfigurationFault.UnknownField:
                configuration[UnknownField] = Invalid;
                break;
            case ProtocolConfigurationFault.NestedField:
                configuration[NestedField] = Invalid;
                break;
            case ProtocolConfigurationFault.SectionValue:
                configuration[AdministratorProtocolConfiguration.Section] = Invalid;
                break;
        }
    }
}

/// <summary>Names independent activation and public policy configuration controls.</summary>
public enum ProtocolConfigurationFault
{
    None, AccountsDisabled, NoProtection, MissingCallback, InsecureCallback, MissingKey, InvalidKey, InvalidSwitch, UnknownField, NestedField, SectionValue
}
