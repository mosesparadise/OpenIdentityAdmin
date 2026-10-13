using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdentityPlatform.Governance.Driver.Features.Protocol;

/// <summary>Owns bounded newline framing and fixed output; raw input and exception diagnostics never leave the child.</summary>
internal static class DriverProtocol
{
    private static readonly byte[] AcknowledgementBytes = "ack\n"u8.ToArray();
    private const byte Newline = (byte)'\n';
    private const string ReadyFrame = "{\"phase\":\"ready\"}\n";
    private const string CommittedFrame = "{\"phase\":\"committed\"}\n";
    private const string FailureFrame = "{\"phase\":\"unavailable\"}\n";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = DriverLimits.JsonDepth
    };

    /// <summary>Reads one bounded byte frame, rejects duplicate properties and discards its raw buffer after deserialization.</summary>
    internal static async Task<DriverInput> ReadAsync(Stream input, CancellationToken token)
    {
        var bytes = new byte[DriverLimits.FrameBytes];
        try
        {
            var count = await FillAsync(input, bytes, token);
            using var document = JsonDocument.Parse(bytes.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = DriverLimits.JsonDepth });
            RequireUnique(document.RootElement);
            var request = document.RootElement.Deserialize<DriverInput>(Options) ?? throw new InvalidOperationException();
            request.RequireShape();
            return request;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Never allocates in proportion to attacker-supplied length or reads the later acknowledgement prematurely.</summary>
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
                return index;
            }
        }
        throw new InvalidOperationException();
    }

    /// <summary>Rejects repeated keys at every nested level instead of accepting last-value-wins command ambiguity.</summary>
    private static void RequireUnique(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidOperationException();
                }
                RequireUnique(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            throw new InvalidOperationException();
        }
    }

    /// <summary>Publishes only a predeclared safe milestone after its corresponding actual action.</summary>
    internal static Task ReadyAsync(CancellationToken token) => WriteAsync(ReadyFrame, token);

    /// <summary>Marks known native accepted completion for a test observer, before delivery of the final command result.</summary>
    internal static Task CommittedAsync(CancellationToken token) => WriteAsync(CommittedFrame, token);

    /// <summary>Reports no exception detail and makes no rollback or noncommit claim.</summary>
    internal static Task UnavailableAsync(CancellationToken token) => WriteAsync(FailureFrame, token);

    /// <summary>Writes a safe frame and flushes it before the parent may act on the milestone.</summary>
    internal static async Task WriteAsync(string frame, CancellationToken token)
    {
        await Console.Out.WriteAsync(frame.AsMemory(), token);
        await Console.Out.FlushAsync(token);
    }

    /// <summary>Accepts exactly the fixed acknowledgement frame without extending the original process deadline.</summary>
    internal static async Task AcknowledgeAsync(Stream input, CancellationToken token)
    {
        ReadOnlyMemory<byte> expected = AcknowledgementBytes;
        var actual = new byte[expected.Length];
        await input.ReadExactlyAsync(actual, token);
        if (!actual.AsSpan().SequenceEqual(expected.Span))
        {
            throw new InvalidOperationException();
        }
    }
}
