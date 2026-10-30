using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Bounds actual bytes and rejects ambiguous JSON before deserializing finite application contracts.</summary>
internal static class ApplicationJson
{
    private const int BufferBytes = 8192;
    private const int MaximumDepth = 16;
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = MaximumDepth
    };

    /// <summary>Reads to EOF within the original cancellation budget, independently of advertised content length.</summary>
    internal static async Task<byte[]> ReadAsync(Stream stream, int maximum, CancellationToken token)
    {
        using var output = new MemoryStream();
        var buffer = new byte[BufferBytes];
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) != 0)
        {
            if (output.Length + count > maximum)
            {
                throw new InvalidDataException();
            }
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    /// <summary>Rejects duplicate names at every depth before the serializer can select a misleading last value.</summary>
    internal static T Parse<T>(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = MaximumDepth });
        RequireUnique(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes, Options) ?? throw new InvalidDataException();
    }

    /// <summary>Preserves exact property spelling and object cardinality throughout nested DTOs.</summary>
    private static void RequireUnique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException();
                }
                RequireUnique(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                RequireUnique(item);
            }
        }
    }
}
