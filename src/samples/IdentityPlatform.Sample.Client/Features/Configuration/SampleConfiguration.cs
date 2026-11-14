using System.Security.Cryptography;

namespace IdentityPlatform.Sample.Client.Features.Configuration;

/// <summary>Loads one immutable private configuration snapshot before any native authentication options are resolved.</summary>
internal sealed class SampleConfiguration(string path = SampleProfile.ConfigurationPath)
{
    private const string ExtensionSeparator = ".";
    private const string CompactGuidFormat = "N";

    private readonly Lock gate = new();
    private SampleSettings? settings;

    /// <summary>Returns null while setup is absent; invalid input fails closed and never constructs native client options.</summary>
    internal SampleSettings? Read()
    {
        lock (gate)
        {
            if (settings is not null || !File.Exists(path))
            {
                return settings;
            }
            using var input = File.OpenRead(path);
            if (input.Length is 0 or > SampleSettings.InputLimit)
            {
                throw new InvalidOperationException(SampleProfile.Failure);
            }
            var bytes = new byte[checked((int)input.Length)];
            try
            {
                input.ReadExactly(bytes);
                settings = SampleSettings.Parse(bytes);
                return settings;
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
    }

    /// <summary>Accepts bounded Docker stdin and atomically publishes a non-overwritable private Linux tmpfs file.</summary>
    internal static async Task ConfigureAsync(Stream input, CancellationToken cancellationToken, string path = SampleProfile.ConfigurationPath)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        var bytes = new byte[SampleSettings.InputLimit + 1];
        var temporary = path + ExtensionSeparator + Guid.NewGuid().ToString(CompactGuidFormat);
        try
        {
            var count = await input.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken);
            _ = SampleSettings.Parse(bytes.AsMemory(0, count));
            await WritePrivateAsync(temporary, bytes.AsMemory(0, count), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            File.Delete(temporary);
        }
    }

    /// <summary>Creates only a new user-readable private file; the parent is the run-owned0700 tmpfs directory.</summary>
    private static async Task WritePrivateAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        await using var output = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        });
        await output.WriteAsync(bytes, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }
}
