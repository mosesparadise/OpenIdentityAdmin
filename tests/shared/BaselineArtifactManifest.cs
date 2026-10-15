using System.Security.Cryptography;
using System.Text.Json;

namespace IdentityPlatform.Testing;

/// <summary>Binds the compatibility process to the complete bounded executable set produced from the frozen source.</summary>
internal static class BaselineArtifactManifest
{
    private const string Baseline = "38eb41e06d40b6addbe57e3af839f912645e6dea";
    private const string BaselineProperty = "baseline";
    private const string FilesProperty = "files";
    private const string InvalidManifest = "Frozen baseline executable evidence is invalid.";
    private const int MaximumManifestBytes = 1024 * 1024;
    private const int MaximumFiles = 500;
    private const long MaximumBytes = 512 * 1024 * 1024;

    /// <summary>Rejects changed, missing, extra or escaping artifacts before the child process starts.</summary>
    internal static void Require(string executable, string manifestPath)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        if (new FileInfo(manifestPath).Length > MaximumManifestBytes)
        {
            throw new InvalidOperationException(InvalidManifest);
        }
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (document.RootElement.GetProperty(BaselineProperty).GetString() != Baseline)
        {
            throw new InvalidOperationException(InvalidManifest);
        }
        var expected = document.RootElement.GetProperty(FilesProperty).EnumerateObject().ToArray();
        var actual = ReadFiles(root);
        if (expected.Length == 0 || expected.Length > MaximumFiles || actual.Count != expected.Length)
        {
            throw new InvalidOperationException(InvalidManifest);
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in expected)
        {
            var path = Path.GetFullPath(Path.Combine(root, entry.Name));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !names.Add(path) || !File.Exists(path))
            {
                throw new InvalidOperationException(InvalidManifest);
            }
            using var stream = File.OpenRead(path);
            var digest = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(digest, entry.Value.GetString(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(InvalidManifest);
            }
        }
        if (!names.Contains(Path.GetFullPath(executable)))
        {
            throw new InvalidOperationException(InvalidManifest);
        }
    }

    /// <summary>Enumerates a bounded artifact tree without following file or directory links.</summary>
    private static List<string> ReadFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        var files = new List<string>();
        long bytes = 0;
        var directories = 0;
        while (pending.TryPop(out var directory))
        {
            if (++directories > MaximumFiles || File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException(InvalidManifest);
            }
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(path);
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    throw new InvalidOperationException(InvalidManifest);
                }
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    if (pending.Count >= MaximumFiles)
                    {
                        throw new InvalidOperationException(InvalidManifest);
                    }
                    pending.Push(path);
                    continue;
                }
                bytes += new FileInfo(path).Length;
                files.Add(path);
                if (files.Count > MaximumFiles || bytes > MaximumBytes)
                {
                    throw new InvalidOperationException(InvalidManifest);
                }
            }
        }
        return files;
    }

}
