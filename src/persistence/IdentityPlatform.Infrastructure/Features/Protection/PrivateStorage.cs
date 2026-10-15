
namespace IdentityPlatform.Infrastructure.Features.Protection;

/// <summary>Shares strict filesystem mechanics without sharing caller credentials or rings.</summary>
internal static class PrivateStorage
{
    private const UnixFileMode PrivateDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const string ProbePrefix = ".probe-";
    private const string ProbeSuffix = ".complete";
    private const string ProbeFormat = "N";
    internal const long MaximumFileBytes = 1_048_576;

    /// <summary>Requires an existing private Linux directory with trusted non-symlink ancestors.</summary>
    internal static string RequireDirectory(string? path)
    {
        var fullPath = RequirePath(path, directory: true);
        if (!OperatingSystem.IsLinux() || File.GetUnixFileMode(fullPath) != PrivateDirectory)
        {
            throw new ProtectionMaterialException();
        }

        return fullPath;
    }

    /// <summary>Requires a private bounded credential file under a private parent.</summary>
    internal static string RequireCredentialFile(string? path)
    {
        var fullPath = RequirePath(path, directory: false);
        if (!OperatingSystem.IsLinux())
        {
            throw new ProtectionMaterialException();
        }

        var mode = File.GetUnixFileMode(fullPath);
        if (mode != UnixFileMode.UserRead && mode != PrivateFile)
        {
            throw new ProtectionMaterialException();
        }
        // Private parent plus operator-trusted ownership/ancestors limits path replacement.
        // Mode checks alone do not establish ownership or eliminate same-identity races.
        RequireDirectory(Path.GetDirectoryName(fullPath));
        RequireBoundedFile(fullPath);
        return fullPath;
    }

    /// <summary>Rejects oversized or symbolic-link ring files before XML parsing.</summary>
    internal static void RequireRingFile(string path)
    {
        RequirePath(path, directory: false);
        RequireBoundedFile(path);
    }

    /// <summary>Reads one bounded stable file and rejects concurrent growth.</summary>
    internal static byte[] ReadBoundedFile(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length is <= 0 or > MaximumFileBytes)
        {
            throw new ProtectionMaterialException();
        }

        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new ProtectionMaterialException();
        }

        return bytes;
    }

    /// <summary>Proves durable create/flush/rename capability and removes only its own random probe.</summary>
    internal static void VerifyWritableDirectory(string directory)
    {
        RequireDirectory(directory);
        var probePath = Path.Combine(directory, ProbePrefix + Guid.NewGuid().ToString(ProbeFormat));
        var renamed = probePath + ProbeSuffix;
        try
        {
            using (var stream = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.WriteByte(1);
                stream.Flush(flushToDisk: true);
            }
            File.Move(probePath, renamed);
            if (!File.ReadAllBytes(renamed).AsSpan().SequenceEqual(new byte[] { 1 }))
            {
                throw new ProtectionMaterialException();
            }
        }
        finally
        {
            File.Delete(probePath);
            File.Delete(renamed);
        }
    }

    /// <summary>Rejects empty and oversized persisted protection material.</summary>
    private static void RequireBoundedFile(string path)
    {
        var length = new FileInfo(path).Length;
        if (length is <= 0 or > MaximumFileBytes)
        {
            throw new ProtectionMaterialException();
        }
    }

    /// <summary>Validates absolute file type and every ancestor without following symbolic links.</summary>
    private static string RequirePath(string? path, bool directory)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ProtectionMaterialException();
        }

        var fullPath = Path.GetFullPath(path);
        var attributes = File.GetAttributes(fullPath);
        if (attributes.HasFlag(FileAttributes.Directory) != directory)
        {
            throw new ProtectionMaterialException();
        }

        for (var current = fullPath; current is not null; current = Path.GetDirectoryName(current))
        {
            var currentAttributes = File.GetAttributes(current);
            if (currentAttributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new ProtectionMaterialException();
            }

            if (OperatingSystem.IsLinux() && currentAttributes.HasFlag(FileAttributes.Directory))
            {
                var mode = File.GetUnixFileMode(current);
                // Permit standard sticky ancestors such as /tmp; their owner must still be trusted.
                if ((mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0 && (mode & UnixFileMode.StickyBit) == 0)
                {
                    throw new ProtectionMaterialException();
                }
            }
        }
        return fullPath;
    }
}
