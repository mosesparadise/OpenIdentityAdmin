using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdentityPlatform.NativeClient.Driver.Features.Protocol;

/// <summary>Owns one bounded private input/output frame and rejects ambiguous or extra input before any native services start.</summary>
internal static class NativeClientDriverProtocol
{
    internal const int FrameBytes = 65_536;
    internal const int ProcessSeconds = 60;
    internal const int FailureExit = 2;
    private const int Depth = 8;
    private const byte Newline = (byte)'\n';
    private static readonly byte[] Terminator = [Newline];
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = Depth
    };

    /// <summary>Reads through EOF before execution; duplicate fields, arrays, oversized frames and trailing data fail without echoing input.</summary>
    internal static async Task<NativeClientProcessInput> ReadAsync(CancellationToken token)
    {
        using var input = Console.OpenStandardInput();
        var bytes = new byte[FrameBytes];
        try
        {
            var length = await FillAsync(input, bytes, token);
            using var document = JsonDocument.Parse(bytes.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = Depth });
            RequireUnique(document.RootElement);
            var result = document.RootElement.Deserialize<NativeClientProcessInput>(Options) ?? throw new InvalidOperationException();
            NativeClientDriverInput.Require(result);
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Bounds frame allocation independently of caller bytes and requires the parent to close its single request stream.</summary>
    private static async Task<int> FillAsync(Stream input, byte[] bytes, CancellationToken token)
    {
        for (var index = 0; index < bytes.Length; index++)
        {
            if (await input.ReadAsync(bytes.AsMemory(index, 1), token) != 1)
            {
                throw new InvalidOperationException();
            }
            if (bytes[index] == Newline)
            {
                var trailing = new byte[1];
                if (await input.ReadAsync(trailing, token) != 0)
                {
                    throw new InvalidOperationException();
                }
                return index;
            }
        }
        throw new InvalidOperationException();
    }

    /// <summary>Rejects duplicate keys recursively before deserialization can silently choose one credential or profile value.</summary>
    private static void RequireUnique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidOperationException();
                }
                RequireUnique(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>Writes bounded private material without console logging, then clears the transient UTF-8 buffer.</summary>
    internal static async Task WriteAsync<T>(T value, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            if (bytes.Length >= FrameBytes)
            {
                throw new InvalidOperationException();
            }
            using var output = Console.OpenStandardOutput();
            await output.WriteAsync(bytes, token);
            await output.WriteAsync(Terminator, token);
            await output.FlushAsync(token);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
