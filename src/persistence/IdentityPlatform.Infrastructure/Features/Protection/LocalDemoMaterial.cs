using System.Security.Cryptography;
using IdentityPlatform.Core.Features.Configuration;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Infrastructure.Features.Protection;

/// <summary>Stages only fixed caller-owned encrypted demo roles beneath an already private Linux mount.</summary>
internal sealed class LocalDemoMaterial : IDisposable
{
    private const string RingDirectory = "ring";
    private const string PfxExtension = ".pfx";
    private const string PathSuffix = ":Path";
    private const string PasswordSuffix = ":Password";
    private const int MinimumPasswordLength = 16;
    private const int MaximumPasswordLength = 256;
    private const int MaximumEncodedLength = 65536;
    private const string EncodedSetting = "Pfx";
    private const string PasswordSetting = "Password";
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private readonly HashSet<string> fingerprints = new(StringComparer.Ordinal);
    private readonly List<string> files = [];
    private readonly List<string> directories = [];
    internal Dictionary<string, string?> Settings { get; } = new(StringComparer.Ordinal);

    /// <summary>Validates the complete bounded role set before creating files; partial writes have a single cleanup owner.</summary>
    internal static LocalDemoMaterial Create(IConfiguration configuration, string root, IReadOnlyDictionary<string, string> roles, string ringSetting)
    {
        var material = new LocalDemoMaterial();
        try
        {
            if (!OperatingSystem.IsLinux())
            {
                throw new LocalDemoConfigurationException();
            }
            PrivateStorage.RequireDirectory(root);
            var section = configuration.GetSection(LocalDemoProfile.MaterialSection);
            if (section.Value is not null || section.GetChildren().Count() != roles.Count ||
                section.GetChildren().Any(child => !roles.ContainsKey(child.Key)))
            {
                throw new LocalDemoConfigurationException();
            }
            foreach (var role in roles)
            {
                material.Write(root, role.Key, role.Value, section.GetSection(role.Key));
            }
            var ring = Path.Combine(root, RingDirectory);
            if (Path.Exists(ring))
            {
                throw new LocalDemoConfigurationException();
            }
            Directory.CreateDirectory(ring, DirectoryMode);
            material.directories.Add(ring);
            material.Settings.Add(ringSetting, PrivateStorage.RequireDirectory(ring));
            return material;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or FormatException or CryptographicException or NotSupportedException or LocalDemoConfigurationException)
        {
            try
            {
                material.Dispose();
            }
            catch (LocalDemoConfigurationException) { }
            throw new LocalDemoConfigurationException();
        }
    }

    /// <summary>Creates a new private encrypted PFX without overwrite; clears decoded bytes even if the write fails.</summary>
    private void Write(string root, string role, string target, IConfigurationSection section)
    {
        if (!OperatingSystem.IsLinux() || section.Value is not null || section.GetChildren().Any(child =>
            child.Key is not EncodedSetting and not PasswordSetting || child.GetChildren().Any()) ||
            section[EncodedSetting] is not { Length: > 0 and <= MaximumEncodedLength } encoded ||
            section[PasswordSetting] is not { Length: >= MinimumPasswordLength and <= MaximumPasswordLength } password)
        {
            throw new LocalDemoConfigurationException();
        }
        var bytes = Convert.FromBase64String(encoded);
        try
        {
            var path = Path.Combine(root, role + PfxExtension);
            using (var file = new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = UnixFileMode.UserRead
            }))
            {
                files.Add(path);
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            using var certificate = ProtectedCertificate.Load(path, password);
            if (!fingerprints.Add(Convert.ToHexString(SHA256.HashData(certificate.GetPublicKey()))))
            {
                throw new LocalDemoConfigurationException();
            }
            Settings.Add(target + PathSuffix, PrivateStorage.RequireCredentialFile(path));
            Settings.Add(target + PasswordSuffix, password);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Removes only this owner's created files and ring after consumers dispose; cleanup never erases unrelated targets.</summary>
    public void Dispose()
    {
        var failed = false;
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failed = true; }
        }
        foreach (var directory in directories)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failed = true; }
        }
        files.Clear();
        directories.Clear();
        fingerprints.Clear();
        Settings.Clear();
        if (failed)
        {
            throw new LocalDemoConfigurationException();
        }
    }
}
