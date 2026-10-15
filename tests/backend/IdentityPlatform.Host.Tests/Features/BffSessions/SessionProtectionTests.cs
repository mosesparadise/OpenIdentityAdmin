using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Host.Tests.Features.BffSessions;

/// <summary>Requires isolated durable encrypted BFF keys, safe initialization failures and retained rotation decryptors.</summary>
public sealed class SessionProtectionTests
{
    /// <summary>Names independently invalid startup configuration shapes.</summary>
    public enum ConfigurationDamage
    {
        MissingApplication, InvalidApplication, MissingRing, RelativeRing, PublicRing, Password, UnknownField, ScalarCurrent, CorruptRing, PublicCredential
    }
    private const string Application = "Bff:Sessions:Protection:ApplicationId";
    private const string Ring = "Bff:Sessions:Protection:KeyRingPath";
    private const string CurrentPath = "Bff:Sessions:Protection:Current:Path";
    private const string CurrentPassword = "Bff:Sessions:Protection:Current:Password";
    private const string KeyPattern = "*.xml";
    private const string CurrentIssuerWrapper = "Identity:Protection:DataProtection:Current:Path";
    private const string CurrentIssuerPassword = "Identity:Protection:DataProtection:Current:Password";
    private const string PreviousIssuerPath = "Identity:Protection:DataProtection:Previous:0:Path";
    private const string PreviousIssuerPassword = "Identity:Protection:DataProtection:Previous:0:Password";

    /// <summary>Proves separate application identity, retained wrapper decryption and failure after actual key loss.</summary>
    [Fact]
    public void Given_DurableTicket_When_WrappersRotateOrKeysDisappear_Then_OnlyRetainedBoundaryDecrypts()
    {
        using var fixture = new BffSessionFixture();
        var row = BffSessionFixture.Row();
        using (var first = fixture.Protection())
        {
            row.ProtectedTicket = new SessionTicketCodec(first.Provider).Protect(row, BffSessionFixture.Ticket());
        }
        fixture.Material.RetainCurrentWrapper();
        using (var rotated = fixture.Protection())
        {
            Assert.NotNull(new SessionTicketCodec(rotated.Provider).Unprotect(row));
        }
        var otherSettings = fixture.Settings();
        const string OtherApplication = "synthetic-other-deployment";
        otherSettings[Application] = OtherApplication;
        using (var other = SessionProtection.Create(new ConfigurationBuilder().AddInMemoryCollection(otherSettings).Build()))
        {
            Assert.Null(new SessionTicketCodec(other.Provider).Unprotect(row));
        }
        fixture.Material.Settings.Remove(PreviousIssuerPath);
        fixture.Material.Settings.Remove(PreviousIssuerPassword);
        var missing = Assert.Throws<SessionProtectionException>(() => fixture.Protection());
        Assert.Equal(SessionProtectionException.SafeMessage, missing.Message);
        Assert.Null(missing.InnerException);
        foreach (var file in Directory.GetFiles(fixture.Material.Ring, KeyPattern))
        {
            File.Delete(file);
        }
        using var lost = fixture.Protection();
        Assert.Null(new SessionTicketCodec(lost.Provider).Unprotect(row));
    }

    /// <summary>Rejects unsafe or incomplete configuration without emitting private path/password/underlying exception details.</summary>
    [Theory]
    [InlineData(ConfigurationDamage.MissingApplication)]
    [InlineData(ConfigurationDamage.InvalidApplication)]
    [InlineData(ConfigurationDamage.MissingRing)]
    [InlineData(ConfigurationDamage.RelativeRing)]
    [InlineData(ConfigurationDamage.PublicRing)]
    [InlineData(ConfigurationDamage.Password)]
    [InlineData(ConfigurationDamage.UnknownField)]
    [InlineData(ConfigurationDamage.ScalarCurrent)]
    [InlineData(ConfigurationDamage.CorruptRing)]
    [InlineData(ConfigurationDamage.PublicCredential)]
    public void Given_UnsafeConfiguration_When_Initialized_Then_FixedSafeFailure(ConfigurationDamage damage)
    {
        using var fixture = new BffSessionFixture();
        var settings = fixture.Settings();
        Damage(fixture, settings, damage);
        var failure = Assert.Throws<SessionProtectionException>(() =>
            SessionProtection.Create(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
        Assert.Equal("Invalid BFF session protection configuration or key ring.", failure.Message);
        Assert.Null(failure.InnerException);
    }

    /// <summary>Rejects expired active wrappers while allowing expired historical decryptors to retain old state.</summary>
    [Fact]
    public void Given_ExpiredActiveWrapper_When_Initialized_Then_NoUsableProviderExists()
    {
        using var fixture = new BffSessionFixture();
        const string WrapperSection = "DataProtection:Current";
        const int PastDays = -2;
        fixture.Material.AddCertificate(WrapperSection, X509KeyUsageFlags.KeyEncipherment,
            notBefore: DateTimeOffset.UtcNow.AddDays(PastDays), notAfter: DateTimeOffset.UtcNow.AddDays(-1));
        Assert.Throws<SessionProtectionException>(() => fixture.Protection());
    }

    /// <summary>Applies one synthetic corruption independently so each rejection has an unambiguous cause.</summary>
    private static void Damage(BffSessionFixture fixture, Dictionary<string, string?> settings, ConfigurationDamage damage)
    {
        const string Invalid = "invalid/value";
        const string WrongPassword = "wrong-synthetic-password";
        const string UnknownField = "Bff:Sessions:Protection:Unexpected";
        const string Current = "Bff:Sessions:Protection:Current";
        const string CorruptFile = "corrupt.xml";
        const string PlaintextXml = "<key version=\"1\"><masterKey>synthetic</masterKey></key>";
        switch (damage)
        {
            case ConfigurationDamage.MissingApplication:
                settings.Remove(Application);
                break;
            case ConfigurationDamage.InvalidApplication:
                settings[Application] = Invalid;
                break;
            case ConfigurationDamage.MissingRing:
                settings.Remove(Ring);
                break;
            case ConfigurationDamage.RelativeRing:
                settings[Ring] = CorruptFile;
                break;
            case ConfigurationDamage.PublicRing:
                SetPublicMode(fixture.Material.Ring);
                break;
            case ConfigurationDamage.Password:
                settings[CurrentPassword] = WrongPassword;
                break;
            case ConfigurationDamage.UnknownField:
                settings[UnknownField] = WrongPassword;
                break;
            case ConfigurationDamage.ScalarCurrent:
                settings[Current] = WrongPassword;
                break;
            case ConfigurationDamage.CorruptRing:
                File.WriteAllText(Path.Combine(fixture.Material.Ring, CorruptFile), PlaintextXml);
                break;
            case ConfigurationDamage.PublicCredential:
                SetPublicMode(settings[CurrentPath]!);
                break;
        }
    }

    /// <summary>Introduces a test-only permission violation on supported Linux filesystems.</summary>
    private static void SetPublicMode(string path)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException();
        }
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherRead);
    }
}
