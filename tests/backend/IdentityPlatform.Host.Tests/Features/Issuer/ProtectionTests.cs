using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Host.Tests.Features.Issuer;

public sealed class ProtectionTests
{
    [Fact]
    public void Persisted_payload_survives_disposal_and_independent_instance_with_only_encrypted_master_keys()
    {
        using var fixture = new IssuerMaterialFixture();
        string payload;
        using (var first = CredentialTests.CreateProvider(fixture))
        {
            first.VerifyIssuerProtection();
            payload = Protector(first).Protect("synthetic-state");
        }
        AssertEncryptedRing(fixture);
        using var second = CredentialTests.CreateProvider(fixture);
        second.VerifyIssuerProtection();
        Assert.Equal("synthetic-state", Protector(second).Unprotect(payload));
        Assert.Throws<CryptographicException>(() => Protector(second, "other-purpose").Unprotect(payload));
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/') + new string('=', (4 - payload.Length % 4) % 4));
        bytes[^1] ^= 1;
        var tampered = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Throws<CryptographicException>(() => Protector(second).Unprotect(tampered));
        fixture.Settings["Identity:Issuer"] = "https://different.example.test/";
        using var different = CredentialTests.CreateProvider(fixture);
        different.VerifyIssuerProtection();
        Assert.Throws<CryptographicException>(() => Protector(different).Unprotect(payload));
        foreach (var file in Directory.GetFiles(fixture.Ring)) File.Delete(file);
        using var lost = CredentialTests.CreateProvider(fixture);
        lost.VerifyIssuerProtection();
        Assert.Throws<CryptographicException>(() => Protector(lost).Unprotect(payload));
    }

    [Fact]
    public void New_wrapper_and_new_master_key_preserve_both_generations_after_restart()
    {
        using var fixture = new IssuerMaterialFixture();
        string oldPayload;
        using (var first = CredentialTests.CreateProvider(fixture))
        {
            first.VerifyIssuerProtection();
            oldPayload = Protector(first).Protect("before-rollover");
        }
        fixture.RetainCurrentWrapper();
        string newPayload;
        Guid newKeyId;
        using (var rotated = CredentialTests.CreateProvider(fixture))
        {
            // Create the second generation before the provider caches its default key.
            var manager = rotated.GetRequiredService<IKeyManager>();
            var activeAt = DateTimeOffset.UtcNow;
            Assert.True(activeAt > manager.GetAllKeys().Single().ActivationDate);
            newKeyId = manager.CreateNewKey(activeAt, activeAt.AddDays(90)).KeyId;
            rotated.VerifyIssuerProtection();
            newPayload = Protector(rotated).Protect("after-rollover");
            Assert.Equal(newKeyId, PayloadKeyId(newPayload));
            Assert.NotEqual(PayloadKeyId(oldPayload), PayloadKeyId(newPayload));
            var persisted = XElement.Load(Path.Combine(fixture.Ring, $"key-{newKeyId:D}.xml"));
            var wrappingCertificate = persisted.Descendants(XName.Get("X509Certificate", "http://www.w3.org/2000/09/xmldsig#")).Single();
            using var publicWrapper = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(wrappingCertificate.Value));
            Assert.Equal(rotated.GetRequiredService<IssuerCredentials>().Wrapping.Thumbprint, publicWrapper.Thumbprint);
            Assert.Equal("before-rollover", Protector(rotated).Unprotect(oldPayload));
        }
        Assert.Equal(2, Directory.GetFiles(fixture.Ring, "key-*.xml").Length);
        AssertEncryptedRing(fixture);
        using (var restart = CredentialTests.CreateProvider(fixture))
        {
            restart.VerifyIssuerProtection();
            Assert.Equal("before-rollover", Protector(restart).Unprotect(oldPayload));
            Assert.Equal("after-rollover", Protector(restart).Unprotect(newPayload));
        }
        fixture.Settings.Remove("Identity:Protection:DataProtection:Previous:0:Path");
        fixture.Settings.Remove("Identity:Protection:DataProtection:Previous:0:Password");
        CredentialTests.AssertSafeFailure(fixture); // A fresh provider must not hide a lost historical decryptor.
    }

    [Fact]
    public void Expired_historical_wrapper_can_decrypt_but_cannot_be_active()
    {
        using var fixture = new IssuerMaterialFixture();
        fixture.AddCertificate("DataProtection:Previous:0", X509KeyUsageFlags.KeyEncipherment,
            notBefore: DateTimeOffset.UtcNow.AddDays(-2), notAfter: DateTimeOffset.UtcNow.AddDays(-1));
        var oldPath = fixture.Settings["Identity:Protection:DataProtection:Previous:0:Path"]!;
        var oldPassword = fixture.Settings["Identity:Protection:DataProtection:Previous:0:Password"]!;
        using var old = X509CertificateLoader.LoadPkcs12FromFile(oldPath, oldPassword, X509KeyStorageFlags.EphemeralKeySet);
        var oldServices = new ServiceCollection();
        oldServices.AddDataProtection().SetApplicationName("IdentityPlatform.AuthServer|" + fixture.Settings["Identity:Issuer"])
            .PersistKeysToFileSystem(new DirectoryInfo(fixture.Ring)).ProtectKeysWithCertificate(old);
        string payload;
        using (var provider = oldServices.BuildServiceProvider()) payload = Protector(provider).Protect("historical-state");
        using (var restored = CredentialTests.CreateProvider(fixture))
        {
            restored.VerifyIssuerProtection();
            Assert.Equal("historical-state", Protector(restored).Unprotect(payload));
        }
        fixture.Settings["Identity:Protection:DataProtection:Current:Path"] = oldPath;
        fixture.Settings["Identity:Protection:DataProtection:Current:Password"] = oldPassword;
        fixture.Settings.Remove("Identity:Protection:DataProtection:Previous:0:Path");
        fixture.Settings.Remove("Identity:Protection:DataProtection:Previous:0:Password");
        CredentialTests.AssertSafeFailure(fixture);
    }

    [Theory]
    [InlineData("plaintext")]
    [InlineData("malformed")]
    [InlineData("unknown-root")]
    [InlineData("invalid-key")]
    [InlineData("oversized")]
    [InlineData("wrong-wrapper")]
    [InlineData("duplicate-key")]
    [InlineData("missing-date")]
    [InlineData("invalid-date")]
    [InlineData("invalid-revocation")]
    [InlineData("specific-logging")]
    [InlineData("missing-deserializer")]
    [InlineData("unknown-deserializer")]
    [InlineData("missing-decryptor")]
    [InlineData("unknown-decryptor")]
    [InlineData("misplaced-secret")]
    [InlineData("extra-child")]
    [InlineData("corrupt-revoked")]
    public void Damaged_or_unprotected_ring_fails_without_logging_xml_or_generating_replacement(string scenario)
    {
        using var fixture = new IssuerMaterialFixture();
        if (scenario == "plaintext")
        {
            var services = new ServiceCollection();
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(fixture.Ring));
            using var provider = services.BuildServiceProvider();
            _ = Protector(provider).Protect("synthetic-private-sentinel");
        }
        else if (scenario is "wrong-wrapper" or "duplicate-key" or "missing-date" or "invalid-date" or "specific-logging" or "missing-deserializer" or "unknown-deserializer" or "missing-decryptor" or "unknown-decryptor" or "misplaced-secret" or "extra-child" or "corrupt-revoked")
        {
            using (var provider = CredentialTests.CreateProvider(fixture))
            {
                provider.VerifyIssuerProtection();
                if (scenario == "corrupt-revoked")
                {
                    var manager = provider.GetRequiredService<IKeyManager>();
                    manager.RevokeKey(manager.GetAllKeys().Single().KeyId, "synthetic revocation");
                }
            }
            if (scenario == "specific-logging")
            {
                var file = Directory.GetFiles(fixture.Ring, "*.xml").Single();
                var xml = XElement.Load(file);
                xml.Add(new XComment("synthetic-private-sentinel"));
                xml.Save(file);
                fixture.AddCertificate("DataProtection:Current", X509KeyUsageFlags.KeyEncipherment);
            }
            else if (scenario == "wrong-wrapper") fixture.AddCertificate("DataProtection:Current", X509KeyUsageFlags.KeyEncipherment);
            else if (scenario == "duplicate-key") File.Copy(Directory.GetFiles(fixture.Ring, "*.xml").Single(), Path.Combine(fixture.Ring, "duplicate.xml"));
            else
            {
                var file = Directory.GetFiles(fixture.Ring, "key-*.xml").Single();
                var xml = XElement.Load(file);
                var secret = xml.Descendants(XName.Get("encryptedSecret", "http://schemas.asp.net/2015/03/dataProtection")).Single();
                switch (scenario)
                {
                    case "missing-date": xml.Element("creationDate")!.Remove(); break;
                    case "invalid-date": xml.Element("creationDate")!.Value = "synthetic-private-sentinel"; break;
                    case "missing-deserializer": xml.Element("descriptor")!.Attribute("deserializerType")!.Remove(); break;
                    case "unknown-deserializer": xml.Element("descriptor")!.SetAttributeValue("deserializerType", "Unavailable.Type, Unavailable.Assembly"); break;
                    case "missing-decryptor": secret.Attribute("decryptorType")!.Remove(); break;
                    case "unknown-decryptor": secret.SetAttributeValue("decryptorType", "Unavailable.Type, Unavailable.Assembly"); break;
                    case "misplaced-secret": secret.Remove(); xml.Add(secret); break;
                    case "extra-child": xml.Add(new XElement("unexpected", "synthetic-private-sentinel")); break;
                    case "corrupt-revoked": secret.Descendants(XName.Get("CipherValue", "http://www.w3.org/2001/04/xmlenc#")).Last().Value = "AA=="; break;
                }
                xml.Save(file);
            }
        }
        else
        {
            File.WriteAllText(Path.Combine(fixture.Ring, "damaged.xml"), scenario switch
            {
                "malformed" => "<key>synthetic-private-sentinel",
                "unknown-root" => "<secret>synthetic-private-sentinel</secret>",
                "invalid-revocation" => "<revocation version=\"1\"><key id=\"*\"/><revocationDate>synthetic-private-sentinel</revocationDate></revocation>",
                "invalid-key" => "<key id=\"invalid\"><encryptedSecret>synthetic-private-sentinel</encryptedSecret></key>",
                _ => new string('x', 1_048_577)
            });
        }
        var originalFiles = Directory.GetFiles(fixture.Ring).Length;
        var logs = new CapturedLogger();
        var target = new ServiceCollection();
        target.AddLogging(options => options.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        if (scenario == "specific-logging")
            target.AddLogging(options => options.AddFilter<CapturedLogger>("Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager", LogLevel.Trace));
        using var material = target.AddIssuerProtection(fixture.Configuration);
        using var final = target.BuildServiceProvider();
        Assert.Throws<IssuerProtectionException>(final.VerifyIssuerProtection);
        Assert.Equal(originalFiles, Directory.GetFiles(fixture.Ring).Length);
        Assert.DoesNotContain("synthetic-private-sentinel", logs.Output.ToString());
        Assert.DoesNotContain("<key", logs.Output.ToString());
        Assert.DoesNotContain(fixture.Root, logs.Output.ToString());
    }

    [Fact]
    public void Diagnostic_suppression_survives_category_provider_rules_and_configuration_reload()
    {
        using var fixture = new IssuerMaterialFixture();
        using var configuration = new ConfigurationManager();
        var capture = new CapturedLogger();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddConfiguration(configuration).SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        using var credentials = services.AddIssuerProtection(fixture.Configuration);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILoggerFactory>();
        var logger = factory.CreateLogger("Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager");
        logger.LogDebug("synthetic-private-sentinel");
        configuration["LogLevel:Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager"] = "Trace";
        configuration["Capture:LogLevel:Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager"] = "Trace";
        ((IConfigurationRoot)configuration).Reload();
        logger.LogDebug("synthetic-private-sentinel");
        factory.CreateLogger("UnrelatedFeature").LogInformation("public-positive-control");
        Assert.DoesNotContain("synthetic-private-sentinel", capture.Output.ToString());
        Assert.Contains("public-positive-control", capture.Output.ToString());
    }

    [Theory]
    [InlineData("relative-file")]
    [InlineData("relative-ring")]
    [InlineData("public-file")]
    [InlineData("unreadable-file")]
    [InlineData("public-ring")]
    [InlineData("unwritable-ring")]
    [InlineData("missing-ring")]
    [InlineData("file-symlink")]
    [InlineData("ring-symlink")]
    [InlineData("ancestor-symlink")]
    [InlineData("ring-entry-symlink")]
    public void Unsafe_filesystem_configuration_is_rejected(string scenario)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Issuer filesystem tests require Linux.");
        using var fixture = new IssuerMaterialFixture();
        var signingPath = fixture.Settings["Identity:Protection:Signing:Path"]!;
        switch (scenario)
        {
            case "relative-file": fixture.Settings["Identity:Protection:Signing:Path"] = "relative.pfx"; break;
            case "relative-ring": fixture.Settings["Identity:Protection:DataProtection:KeyRingPath"] = "relative"; break;
            case "public-file": File.SetUnixFileMode(signingPath, UnixFileMode.UserRead | UnixFileMode.OtherRead); break;
            case "unreadable-file": File.SetUnixFileMode(signingPath, UnixFileMode.UserWrite); break;
            case "public-ring": File.SetUnixFileMode(fixture.Ring, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherExecute); break;
            case "unwritable-ring": File.SetUnixFileMode(fixture.Ring, UnixFileMode.UserRead | UnixFileMode.UserExecute); break;
            case "missing-ring": Directory.Delete(fixture.Ring); break;
            case "file-symlink":
                var link = Path.Combine(fixture.Root, "linked.pfx"); File.CreateSymbolicLink(link, signingPath);
                fixture.Settings["Identity:Protection:Signing:Path"] = link; break;
            case "ring-symlink":
                var linkedRing = Path.Combine(fixture.Root, "linked-ring"); Directory.CreateSymbolicLink(linkedRing, fixture.Ring);
                fixture.Settings["Identity:Protection:DataProtection:KeyRingPath"] = linkedRing; break;
            case "ancestor-symlink":
                var parent = Path.Combine(fixture.Root, "linked-parent"); Directory.CreateSymbolicLink(parent, fixture.Root);
                fixture.Settings["Identity:Protection:Signing:Path"] = Path.Combine(parent, Path.GetFileName(signingPath)); break;
            case "ring-entry-symlink": File.CreateSymbolicLink(Path.Combine(fixture.Ring, "key.xml"), signingPath); break;
        }
        CredentialTests.AssertSafeFailure(fixture);
        // Restore owner access for deterministic fixture cleanup, including non-root runners.
        if (Directory.Exists(fixture.Ring)) File.SetUnixFileMode(fixture.Ring, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public void Ring_inventory_bound_rejects_more_than_256_documents()
    {
        using var fixture = new IssuerMaterialFixture();
        using (var provider = CredentialTests.CreateProvider(fixture)) provider.VerifyIssuerProtection();
        var original = XElement.Load(Directory.GetFiles(fixture.Ring, "key-*.xml").Single());
        for (var index = 0; index < 256; index++)
        {
            var copy = new XElement(original);
            var id = Guid.NewGuid();
            copy.SetAttributeValue("id", id);
            copy.Save(Path.Combine(fixture.Ring, $"key-{id:D}.xml"));
        }
        CredentialTests.AssertSafeFailure(fixture);
    }

    [Fact]
    public void Native_revocation_is_preserved_after_reload()
    {
        using var fixture = new IssuerMaterialFixture();
        string payload;
        using (var provider = CredentialTests.CreateProvider(fixture))
        {
            provider.VerifyIssuerProtection();
            payload = Protector(provider).Protect("revoked-state");
            var manager = provider.GetRequiredService<IKeyManager>();
            manager.RevokeKey(manager.GetAllKeys().Single().KeyId, "synthetic test");
        }
        using var reloaded = CredentialTests.CreateProvider(fixture);
        reloaded.VerifyIssuerProtection();
        Assert.Throws<CryptographicException>(() => Protector(reloaded).Unprotect(payload));
    }

    private static Guid PayloadKeyId(string payload)
    {
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/') + new string('=', (4 - payload.Length % 4) % 4));
        return new Guid(bytes.AsSpan(4, 16)); // Pinned ASP.NET Core protected-payload format: magic then key ID.
    }

    private static IDataProtector Protector(IServiceProvider provider, string purpose = "fixture-session") =>
        provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(purpose);

    private static void AssertEncryptedRing(IssuerMaterialFixture fixture)
    {
        var files = Directory.GetFiles(fixture.Ring, "key-*.xml");
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var xml = XElement.Load(file);
            Assert.NotEmpty(xml.Descendants(XName.Get("encryptedSecret", "http://schemas.asp.net/2015/03/dataProtection")));
            Assert.Empty(xml.Descendants("masterKey"));
        }
    }

    [ProviderAlias("Capture")]
    private sealed class CapturedLogger : ILoggerProvider, ILogger
    {
        public StringBuilder Output { get; } = new();
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> format) => Output.AppendLine(format(state, exception));
        public void Dispose() { }
    }
}
