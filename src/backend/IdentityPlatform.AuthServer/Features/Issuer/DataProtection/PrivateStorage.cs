using IdentityPlatform.AuthServer.Features.Issuer.Credentials;

namespace IdentityPlatform.AuthServer.Features.Issuer.DataProtection;

internal static class PrivateStorage
{
    private const UnixFileMode PrivateDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    internal const long MaximumFileBytes = 1_048_576;

    internal static string RequireDirectory(string? path)
    {
        var fullPath = RequirePath(path, directory: true);
        if (!OperatingSystem.IsLinux() || File.GetUnixFileMode(fullPath) != PrivateDirectory)
        {
            throw new IssuerProtectionException();
        }

        return fullPath;
    }

    internal static string RequireCredentialFile(string? path)
    {
        var fullPath = RequirePath(path, directory: false);
        if (!OperatingSystem.IsLinux())
        {
            throw new IssuerProtectionException();
        }

        var mode = File.GetUnixFileMode(fullPath);
        if (mode != UnixFileMode.UserRead && mode != PrivateFile)
        {
            throw new IssuerProtectionException();
        }
        // Private parent plus operator-trusted ownership/ancestors limits path replacement.
        // Mode checks alone do not establish ownership or eliminate same-identity races.
        RequireDirectory(Path.GetDirectoryName(fullPath));
        RequireBoundedFile(fullPath);
        return fullPath;
    }

    internal static void RequireRingFile(string path)
    {
        RequirePath(path, directory: false);
        RequireBoundedFile(path);
    }

    internal static byte[] ReadBoundedFile(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length is <= 0 or > MaximumFileBytes)
        {
            throw new IssuerProtectionException();
        }

        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new IssuerProtectionException();
        }

        return bytes;
    }

    internal static void VerifyWritableDirectory(string directory)
    {
        RequireDirectory(directory);
        var probePath = Path.Combine(directory, ".probe-" + Guid.NewGuid().ToString("N"));
        var renamed = probePath + ".complete";
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
                throw new IssuerProtectionException();
            }
        }
        finally
        {
            File.Delete(probePath);
            File.Delete(renamed);
        }
    }

    private static void RequireBoundedFile(string path)
    {
        var length = new FileInfo(path).Length;
        if (length is <= 0 or > MaximumFileBytes)
        {
            throw new IssuerProtectionException();
        }
    }

    private static string RequirePath(string? path, bool directory)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new IssuerProtectionException();
        }

        var fullPath = Path.GetFullPath(path);
        var attributes = File.GetAttributes(fullPath);
        if (attributes.HasFlag(FileAttributes.Directory) != directory)
        {
            throw new IssuerProtectionException();
        }

        for (var current = fullPath; current is not null; current = Path.GetDirectoryName(current))
        {
            var currentAttributes = File.GetAttributes(current);
            if (currentAttributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IssuerProtectionException();
            }

            if (OperatingSystem.IsLinux() && currentAttributes.HasFlag(FileAttributes.Directory))
            {
                var mode = File.GetUnixFileMode(current);
                // Permit standard sticky ancestors such as /tmp; their owner must still be trusted.
                if ((mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0 && (mode & UnixFileMode.StickyBit) == 0)
                {
                    throw new IssuerProtectionException();
                }
            }
        }
        return fullPath;
    }
}
