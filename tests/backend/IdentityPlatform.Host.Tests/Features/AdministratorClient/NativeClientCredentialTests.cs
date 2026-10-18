using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Exercises actual encrypted protected-file loading for client-specific roles without reusing issuer or B1 credentials.</summary>
public sealed class NativeClientCredentialTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string Section = "ClientCredentials";
    private const string StateSignPath = "ClientCredentials:StateSigning:Path";
    private const string StateSignPassword = "ClientCredentials:StateSigning:Password";
    private const string StateEncryptPath = "ClientCredentials:StateEncryption:Path";
    private const string StateEncryptPassword = "ClientCredentials:StateEncryption:Password";
    private const string AssertionPath = "ClientCredentials:AssertionSigning:Path";
    private const string AssertionPassword = "ClientCredentials:AssertionSigning:Password";
    private const string UnknownSetting = "ClientCredentials:AutomaticKeyGeneration";
    private const string ScalarSetting = "ClientCredentials:StateSigning:Previous";
    private const string InvalidPassword = "synthetic-unusable-password";
    private const string Enabled = "true";
    private const int ProbeBytes = 32;

    /// <summary>Names explicit startup failures that must not leak configured paths, passwords or cryptographic diagnostics.</summary>
    public enum Failure
    {
        MissingPath, Password, WrongRole, SharedRole, Unknown, AmbiguousPrevious, Expired, Future
    }

    /// <summary>Separate imports can use the same durable files across restart while retaining distinct roles and ownership.</summary>
    [Fact]
    public void Given_DurableDistinctClientCredentials_When_LoadedTwice_Then_ActualKeysRemainUsableAndIndependentlyOwned()
    {
        using var first = Load(fixture.Settings(), DateTimeOffset.UtcNow);
        using var second = Load(fixture.Settings(), DateTimeOffset.UtcNow);
        Assert.NotSame(first.Assertion, second.Assertion);
        Assert.Equal(first.Assertion.Thumbprint, second.Assertion.Thumbprint);
        Assert.NotEqual(first.Assertion.Thumbprint, first.StateSigning.Active.Thumbprint);
        Assert.NotEqual(first.StateSigning.Active.Thumbprint, first.StateEncryption.Active.Thumbprint);
        var probe = RandomNumberGenerator.GetBytes(ProbeBytes);
        using var signing = first.Assertion.GetRSAPrivateKey();
        using var validation = second.Assertion.GetRSAPublicKey();
        Assert.True(validation!.VerifyData(probe, signing!.SignData(probe, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        first.Dispose();
        using var secondSigning = second.Assertion.GetRSAPrivateKey();
        Assert.True(validation.VerifyData(probe, secondSigning!.SignData(probe, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    /// <summary>All invalid role/configuration/time samples produce only the fixed public failure category without an inner exception.</summary>
    [Theory]
    [InlineData(Failure.MissingPath)]
    [InlineData(Failure.Password)]
    [InlineData(Failure.WrongRole)]
    [InlineData(Failure.SharedRole)]
    [InlineData(Failure.Unknown)]
    [InlineData(Failure.AmbiguousPrevious)]
    [InlineData(Failure.Expired)]
    [InlineData(Failure.Future)]
    public void Given_InvalidClientCredentialProfile_When_Loaded_Then_FailsWithOnlySafeCategory(Failure failure)
    {
        var settings = fixture.Settings();
        var now = DateTimeOffset.UtcNow;
        now = Change(settings, now, failure);
        var error = Assert.Throws<ClientCredentialFailure>(() => Load(settings, now));
        Assert.Equal(ClientCredentialFailure.SafeMessage, error.Message);
        Assert.Null(error.InnerException);
    }

    /// <summary>Mutates one independently selected role/configuration boundary without touching shared synthetic files.</summary>
    private static DateTimeOffset Change(Dictionary<string, string?> settings, DateTimeOffset now, Failure failure)
    {
        switch (failure)
        {
            case Failure.MissingPath:
                settings.Remove(StateSignPath);
                break;
            case Failure.Password:
                settings[StateSignPassword] = InvalidPassword;
                break;
            case Failure.WrongRole:
                settings[StateSignPath] = settings[StateEncryptPath];
                settings[StateSignPassword] = settings[StateEncryptPassword];
                break;
            case Failure.SharedRole:
                settings[AssertionPath] = settings[StateSignPath];
                settings[AssertionPassword] = settings[StateSignPassword];
                break;
            case Failure.Unknown:
                settings[UnknownSetting] = Enabled;
                break;
            case Failure.AmbiguousPrevious:
                settings[ScalarSetting] = Enabled;
                break;
            case Failure.Expired:
                now = DateTimeOffset.MaxValue;
                break;
            case Failure.Future:
                now = DateTimeOffset.MinValue;
                break;
        }
        return now;
    }

    /// <summary>Uses the same explicit credential section contract as future opt-in client composition.</summary>
    private static NativeClientCredentials Load(Dictionary<string, string?> settings, DateTimeOffset now) =>
        NativeClientCredentials.Load(new ConfigurationBuilder().AddInMemoryCollection(settings).Build().GetSection(Section), now);
}
