using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Testing;

namespace IdentityPlatform.Host.Tests.Features.DatabaseProviders;

/// <summary>Ensures frozen compatibility evidence rejects altered executable sets before process launch.</summary>
public sealed class BaselineManifestTests
{
    /// <summary>Independent trust properties altered by this control.</summary>
    public enum ManifestChange
    {
        Valid, Changed, Missing, Extra, Baseline, Escape, Link, Oversize
    }

    private const string Baseline = "38eb41e06d40b6addbe57e3af839f912645e6dea";
    private const string Executable = "producer.dll";
    private const string Manifest = "manifest.json";
    private const string Original = "synthetic original artifact";
    private const string Changed = "synthetic changed artifact";
    private const string Extra = "extra.dll";
    private const string Binaries = "bin";
    private const string Escape = "../outside.dll";

    /// <summary>A complete exact manifest passes; each independent substitution fails closed.</summary>
    [Theory]
    [InlineData(ManifestChange.Valid)]
    [InlineData(ManifestChange.Changed)]
    [InlineData(ManifestChange.Missing)]
    [InlineData(ManifestChange.Extra)]
    [InlineData(ManifestChange.Baseline)]
    [InlineData(ManifestChange.Escape)]
    [InlineData(ManifestChange.Link)]
    [InlineData(ManifestChange.Oversize)]
    public void Given_ArtifactManifest_When_Validated_Then_OnlyExactFrozenSetPasses(ManifestChange variant)
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var binaries = Directory.CreateDirectory(Path.Combine(root.FullName, Binaries));
            var executable = Path.Combine(binaries.FullName, Executable);
            File.WriteAllText(executable, Original);
            var files = new Dictionary<string, string> { [Executable] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executable))) };
            ApplyChange(variant, binaries.FullName, executable, files);
            var manifest = Path.Combine(root.FullName, Manifest);
            File.WriteAllText(manifest, JsonSerializer.Serialize(new
            {
                baseline = variant == ManifestChange.Baseline ? Changed : Baseline,
                files
            }));
            if (variant == ManifestChange.Oversize)
            {
                const int OversizeBytes = 1024 * 1024 + 1;
                using var stream = File.OpenWrite(manifest);
                stream.SetLength(OversizeBytes);
            }
            if (variant == ManifestChange.Valid)
            {
                BaselineArtifactManifest.Require(executable, manifest);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => BaselineArtifactManifest.Require(executable, manifest));
            }
        }
        finally { root.Delete(recursive: true); }
    }

    /// <summary>An expired operation cannot inspect configuration or launch the baseline child.</summary>
    [Fact]
    public async Task Given_CancelledDeadline_When_ProducerIsRequested_Then_CancelsBeforeLaunch()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility.BaselineProducer.PopulateAsync(
                string.Empty, string.Empty, cancellation.Token));
    }

    /// <summary>Changes one trust property at a time while keeping the remaining evidence intact.</summary>
    private static void ApplyChange(ManifestChange variant, string directory, string executable, Dictionary<string, string> files)
    {
        switch (variant)
        {
            case ManifestChange.Changed:
                File.WriteAllText(executable, Changed);
                break;
            case ManifestChange.Missing:
                File.Delete(executable);
                break;
            case ManifestChange.Extra:
                File.WriteAllText(Path.Combine(directory, Extra), Original);
                break;
            case ManifestChange.Link:
                File.Move(executable, Path.Combine(directory, Escape));
                File.CreateSymbolicLink(executable, Path.Combine(directory, Escape));
                break;
            case ManifestChange.Escape:
                File.WriteAllText(Path.Combine(directory, Extra), Original);
                File.WriteAllText(Path.Combine(directory, Escape), Original);
                files[Escape] = files[Executable];
                break;
        }
    }
}
