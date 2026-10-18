using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks immutable fixed native registration policy and rejection of drift before any production composition uses it.</summary>
public sealed class AdministratorRegistrationTests : IClassFixture<AdministratorRegistrationFixture>
{
    private const string Callback = "https://administrator.example.test/callback";
    private const string DifferentCallback = "https://administrator.example.test/other";
    private const int DriftLifetimeMinutes = 5;
    private const string ObjectStart = "{";
    private const string Secret = "unexpected-shared-secret";
    private readonly AdministratorRegistrationFixture fixture;

    /// <summary>Reuses only test-owned public key material while every descriptor remains independent.</summary>
    public AdministratorRegistrationTests(AdministratorRegistrationFixture fixture) => this.fixture = fixture;

    /// <summary>Fresh native descriptors match, and caller mutation cannot change the immutable expected key.</summary>
    [Fact]
    public void Given_FixedPolicy_When_DescriptorMutates_Then_ExpectedKeyRemainsImmutable()
    {
        var policy = AdministratorRegistrationPolicy.Create(Callback, fixture.Key)!;
        Assert.True(policy.Matches(policy.Application(), AdministratorRegistrationPolicy.Scope()));
        var changed = policy.Application();
        changed.JsonWebKeySet!.Keys[0].Kid = Secret;
        Assert.False(policy.Matches(changed, AdministratorRegistrationPolicy.Scope()));
        Assert.True(policy.Matches(policy.Application(), AdministratorRegistrationPolicy.Scope()));
    }

    /// <summary>Each independently changed security boundary invalidates the exact profile.</summary>
    [Theory]
    [InlineData(RegistrationDrift.SharedSecret)]
    [InlineData(RegistrationDrift.ExtraRedirect)]
    [InlineData(RegistrationDrift.RefreshPermission)]
    [InlineData(RegistrationDrift.MissingPar)]
    [InlineData(RegistrationDrift.LongerCode)]
    [InlineData(RegistrationDrift.Disabled)]
    [InlineData(RegistrationDrift.ExtraResource)]
    [InlineData(RegistrationDrift.ClientCase)]
    [InlineData(RegistrationDrift.ExtraKey)]
    public void Given_FixedPolicy_When_SecurityMetadataDrifts_Then_RegistrationIsDenied(RegistrationDrift change)
    {
        var policy = AdministratorRegistrationPolicy.Create(Callback, fixture.Key)!;
        var application = policy.Application();
        var scope = AdministratorRegistrationPolicy.Scope();
        Assert.True(policy.Matches(application, scope));
        Change(application, scope, change);
        Assert.False(policy.Matches(application, scope));
    }

    /// <summary>Applies one independently named registration defect while retaining all other valid fields.</summary>
    private void Change(OpenIddictApplicationDescriptor application, OpenIddictScopeDescriptor scope, RegistrationDrift change)
    {
        switch (change)
        {
            case RegistrationDrift.SharedSecret:
                application.ClientSecret = Secret;
                break;
            case RegistrationDrift.ExtraRedirect:
                application.RedirectUris.Add(new Uri(DifferentCallback));
                break;
            case RegistrationDrift.RefreshPermission:
                application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
                break;
            case RegistrationDrift.MissingPar:
                application.Requirements.Remove(OpenIddictConstants.Requirements.Features.PushedAuthorizationRequests);
                break;
            case RegistrationDrift.LongerCode:
                application.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(DriftLifetimeMinutes));
                break;
            case RegistrationDrift.Disabled:
                application.Properties[AdministratorRegistrationPolicy.EnabledProperty] = JsonSerializer.SerializeToElement(false);
                break;
            case RegistrationDrift.ExtraResource:
                scope.Resources.Add(Secret);
                break;
            case RegistrationDrift.ClientCase:
                application.ClientId = AdministratorProtocolIdentity.ClientId.ToUpperInvariant();
                break;
            case RegistrationDrift.ExtraKey:
                application.JsonWebKeySet!.Keys.Add(fixture.Key.CreateNativeKey());
                break;
        }
    }

    /// <summary>Callback configuration cannot normalize an unsafe or differently serialized destination into the fixed registration.</summary>
    [Theory]
    [InlineData("http://administrator.example.test/callback")]
    [InlineData("https://administrator.example.test/callback#fragment")]
    [InlineData("https://user@administrator.example.test/callback")]
    [InlineData("https://administrator.example.test/*")]
    [InlineData("https://administrator.example.test")]
    public void Given_InvalidCallback_When_Configured_Then_PolicyIsRejected(string callback) =>
        Assert.Null(AdministratorRegistrationPolicy.Create(callback, fixture.Key));

    /// <summary>Public key parsing rejects private fields and duplicate members rather than normalizing them away.</summary>
    [Theory]
    [InlineData("\"d\":\"AQAB\",")]
    [InlineData("\"kid\":\"duplicate\",")]
    [InlineData("\"jku\":\"https://attacker.example.test/keys\",")]
    public void Given_AdditionalKeyMaterial_When_Parsed_Then_KeyIsRejected(string prefix) =>
        Assert.Null(AdministratorPublicKey.Parse(ObjectStart + prefix + fixture.Json[1..]));
}

/// <summary>Provides a disposable synthetic RSA generation step and retains public parameters only.</summary>
public sealed class AdministratorRegistrationFixture
{
    private const int KeyBits = 3072;
    private const string RsaType = "RSA";
    private const string TestKey = "registration-test";
    private const string SignatureUse = "sig";
    internal string Json
    {
        get;
    }
    internal AdministratorPublicKey Key
    {
        get;
    }

    /// <summary>Creates public test configuration matching the operator-owned fixed-key document shape.</summary>
    public AdministratorRegistrationFixture()
    {
        using var rsa = RSA.Create(KeyBits);
        var parameters = rsa.ExportParameters(false);
        Json = JsonSerializer.Serialize(new
        {
            kty = RsaType,
            kid = TestKey,
            alg = SecurityAlgorithms.RsaSha256,
            use = SignatureUse,
            n = Base64UrlEncoder.Encode(parameters.Modulus),
            e = Base64UrlEncoder.Encode(parameters.Exponent)
        });
        Key = AdministratorPublicKey.Parse(Json)!;
        Assert.NotNull(Key);
    }
}

/// <summary>Names independent fixed-registration drift controls.</summary>
public enum RegistrationDrift
{
    SharedSecret, ExtraRedirect, RefreshPermission, MissingPar, LongerCode, Disabled, ExtraResource, ClientCase, ExtraKey
}
