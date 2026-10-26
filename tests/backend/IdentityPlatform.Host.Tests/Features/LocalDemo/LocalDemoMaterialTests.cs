using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Protection;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Host.Tests.Features.LocalDemo;

/// <summary>Seeds unsafe staging and cross-role faults at the real private material adapter.</summary>
public sealed class LocalDemoMaterialTests(LocalDemoFixture fixture) : IClassFixture<LocalDemoFixture>
{
    private const string RingSetting = "Ring";
    private const string WrappingPfx = "LocalDemoMaterial:ClientWrapping:Pfx";
    private const string WrappingPassword = "LocalDemoMaterial:ClientWrapping:Password";
    private const string EncryptionPfx = "LocalDemoMaterial:ClientStateEncryption:Pfx";
    private const string EncryptionPassword = "LocalDemoMaterial:ClientStateEncryption:Password";
    private const string OversizedFault = "oversized";
    private const string SensitiveSentinel = "synthetic-private-sentinel!";
    private const string UnrelatedFile = "owned-by-other";
    private const string UnrelatedContent = "untouched";
    private const string FirstRoleFile = "ClientStateSigning.pfx";
    private const int OversizedLength = 65537;
    private static readonly Dictionary<string, string> Roles = new(StringComparer.Ordinal)
    {
        ["ClientStateSigning"] = "Client:Signing",
        ["ClientStateEncryption"] = "Client:Encryption",
        ["ClientAssertion"] = "Client:Assertion",
        ["ClientWrapping"] = "Client:Wrapping"
    };

    /// <summary>Distinct encrypted roles remain private and all owned material is removed after disposal.</summary>
    [Fact]
    public void Given_ValidMaterial_When_StagedAndDisposed_Then_PrivateFilesAndRingHaveOwnedLifetime()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException();
        }
        var root = fixture.CreateRoot();
        using (var material = Create(fixture.BffSettings(root), root))
        {
            Assert.Equal(4, Directory.GetFiles(root).Length);
            foreach (var file in Directory.GetFiles(root))
            {
                Assert.Equal(UnixFileMode.UserRead, File.GetUnixFileMode(file));
            }
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(material.Settings[RingSetting]!));
        }
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    /// <summary>Cross-loader encryption/wrapping reuse is rejected even though either existing loader accepts the certificate independently.</summary>
    [Fact]
    public void Given_ReusedEncryptionAndWrappingKey_When_Staged_Then_CompleteRoleSetIsRejectedAndCleaned()
    {
        var root = fixture.CreateRoot();
        var settings = fixture.BffSettings(root);
        settings[WrappingPfx] = settings[EncryptionPfx];
        settings[WrappingPassword] = settings[EncryptionPassword];
        AssertSafeFailure(settings, root);
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    /// <summary>Late malformed material removes earlier successful writes without echoing the supplied value.</summary>
    [Theory]
    [InlineData("bad-base64")]
    [InlineData(OversizedFault)]
    public void Given_InvalidLaterRole_When_StagingFails_Then_PartialFilesAreRemoved(string fault)
    {
        var root = fixture.CreateRoot();
        var settings = fixture.BffSettings(root);
        settings[WrappingPfx] = fault == OversizedFault ? new string('A', OversizedLength) : SensitiveSentinel;
        AssertSafeFailure(settings, root);
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    /// <summary>CreateNew prevents overwrite or symlink traversal and cleanup preserves the pre-existing unrelated target.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_ExistingRoleTarget_When_StagingRuns_Then_UnrelatedFileIsPreserved(bool symlink)
    {
        var root = fixture.CreateRoot();
        var external = Path.Combine(fixture.CreateRoot(), UnrelatedFile);
        File.WriteAllText(external, UnrelatedContent);
        var target = Path.Combine(root, FirstRoleFile);
        if (symlink)
        {
            File.CreateSymbolicLink(target, external);
        }
        else
        {
            File.WriteAllText(target, UnrelatedContent);
        }
        AssertSafeFailure(fixture.BffSettings(root), root);
        Assert.Equal(UnrelatedContent, File.ReadAllText(external));
        Assert.Equal(UnrelatedContent, File.ReadAllText(target));
    }

    /// <summary>A writable-by-others parent is rejected before any role file is created.</summary>
    [Fact]
    public void Given_UnsafeRootPermissions_When_StagingRuns_Then_NoPrivateMaterialIsWritten()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException();
        }
        var root = fixture.CreateRoot();
        File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherRead);
        AssertSafeFailure(fixture.BffSettings(root), root);
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    /// <summary>Forces a deletion failure and proves later owned files/ring are still removed with only a finite diagnostic.</summary>
    [Fact]
    public void Given_FirstCleanupTargetCannotBeDeleted_When_Disposed_Then_OtherOwnedMaterialIsStillRemoved()
    {
        var root = fixture.CreateRoot();
        var material = Create(fixture.BffSettings(root), root);
        var first = Path.Combine(root, FirstRoleFile);
        File.Delete(first);
        Directory.CreateDirectory(first);
        var error = Assert.Throws<LocalDemoConfigurationException>(material.Dispose);
        Assert.Equal(LocalDemoProfile.Failure, error.Message);
        Assert.Equal(new[] { first }, Directory.GetFileSystemEntries(root));
        material.Dispose();
    }

    /// <summary>Asserts the fixed diagnostic without printing any transported credential data.</summary>
    private static void AssertSafeFailure(Dictionary<string, string?> settings, string root) =>
        Assert.Equal(LocalDemoProfile.Failure, Assert.Throws<LocalDemoConfigurationException>(() => Create(settings, root)).Message);

    /// <summary>Runs the real adapter with an existing private test directory and fixed caller-owned role mapping.</summary>
    private static LocalDemoMaterial Create(Dictionary<string, string?> settings, string root) =>
        LocalDemoMaterial.Create(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), root, Roles, RingSetting);
}
