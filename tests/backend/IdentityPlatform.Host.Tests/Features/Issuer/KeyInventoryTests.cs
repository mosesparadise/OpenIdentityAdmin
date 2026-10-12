using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Testing;

namespace IdentityPlatform.Host.Tests.Features.Issuer;

public sealed class KeyInventoryTests
{
    [Theory]
    [InlineData("Signing", X509KeyUsageFlags.DigitalSignature)]
    [InlineData("Encryption", X509KeyUsageFlags.KeyEncipherment)]
    public void Staged_and_retained_token_keys_do_not_change_active_or_wrapping_roles(string purpose, X509KeyUsageFlags usage)
    {
        using var fixture = new IssuerMaterialFixture();
        using var baseline = IssuerCredentials.Load(fixture.Configuration, DateTimeOffset.UtcNow);
        fixture.AddCertificate(purpose + ":Staged", usage, notBefore: DateTimeOffset.UtcNow.AddDays(1), notAfter: DateTimeOffset.UtcNow.AddDays(60));
        for (var index = 0; index < 4; index++)
        {
            fixture.AddCertificate($"{purpose}:Previous:{index}", usage,
                notBefore: DateTimeOffset.UtcNow.AddDays(-3), notAfter: DateTimeOffset.UtcNow.AddDays(-1));
        }
        var loaded = IssuerCredentials.Load(fixture.Configuration, DateTimeOffset.UtcNow);
        var inventory = purpose == "Signing" ? loaded.SigningKeys : loaded.EncryptionKeys;
        Assert.Equal(6, inventory.All.Count);
        Assert.Equal(baseline.Signing.Thumbprint, loaded.Signing.Thumbprint);
        Assert.Equal(baseline.Encryption.Thumbprint, loaded.Encryption.Thumbprint);
        Assert.Equal(baseline.Wrapping.Thumbprint, Assert.Single(loaded.WrappingDecryptors).Thumbprint);
        Assert.Equal(4, inventory.Previous.Count);
        Assert.NotNull(inventory.Staged);
        loaded.Dispose();
        Assert.All(inventory.All, certificate => Assert.Throws<CryptographicException>(() => certificate.GetRSAPrivateKey()));
        loaded.Dispose();
        fixture.AddCertificate(purpose + ":Previous:4", usage);
        CredentialTests.AssertSafeFailure(fixture);
    }

    [Theory]
    [InlineData("Staged:Password", "sentinel")]
    [InlineData("Staged:Path", "sentinel")]
    [InlineData("Staged", "sentinel")]
    [InlineData("Staged:Unknown", "sentinel")]
    [InlineData("Previous", "sentinel")]
    [InlineData("Previous:1:Path", "sentinel")]
    [InlineData("Previous:00:Path", "sentinel")]
    [InlineData("Previous:bad:Path", "sentinel")]
    [InlineData("Previous:-1:Path", "sentinel")]
    [InlineData("Active:Path", "sentinel")]
    [InlineData("Path:Unknown", "sentinel")]
    public void Ambiguous_inventory_fails_with_safe_configuration_error(string key, string value)
    {
        using var fixture = new IssuerMaterialFixture();
        fixture.Settings["Identity:Protection:Signing:" + key] = value;
        CredentialTests.AssertSafeFailure(fixture);
    }

    [Theory]
    [InlineData("Staged", -2, -1)]
    [InlineData("Previous:0", 1, 2)]
    public void Staged_expiry_and_future_historical_material_are_rejected(string slot, int starts, int ends)
    {
        using var fixture = new IssuerMaterialFixture();
        fixture.AddCertificate("Signing:" + slot, X509KeyUsageFlags.DigitalSignature,
            notBefore: DateTimeOffset.UtcNow.AddDays(starts), notAfter: DateTimeOffset.UtcNow.AddDays(ends));
        CredentialTests.AssertSafeFailure(fixture);
    }

    [Theory]
    [InlineData("Signing:Staged")]
    [InlineData("Signing:Previous:0")]
    [InlineData("Encryption:Staged")]
    [InlineData("Encryption:Previous:0")]
    public void All_inventory_slots_preserve_duplicate_and_cross_purpose_protection(string slot)
    {
        using var fixture = new IssuerMaterialFixture();
        using var key = RSA.Create(3072);
        fixture.AddCertificate("Signing", X509KeyUsageFlags.DigitalSignature, sharedKey: key);
        fixture.AddCertificate(slot, slot.StartsWith("Signing", StringComparison.Ordinal) ? X509KeyUsageFlags.DigitalSignature : X509KeyUsageFlags.KeyEncipherment, sharedKey: key);
        CredentialTests.AssertSafeFailure(fixture);
    }
}
