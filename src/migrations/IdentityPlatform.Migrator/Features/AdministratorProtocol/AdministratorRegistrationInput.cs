using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

namespace IdentityPlatform.Migrator.Features.AdministratorProtocol;

/// <summary>Reads one bounded explicit initialization document without accepting private keys or ambiguous field names.</summary>
internal sealed record AdministratorRegistrationInput(Guid OperationId, string OperatorReference, AdministratorRegistrationPolicy Policy)
{
    private const int MaximumBytes = 12288;
    private const int MaximumDepth = 3;
    private const int MaximumOperatorLength = 64;
    private const string GuidFormat = "D";
    private const string InvalidMessage = "Invalid administrator protocol initialization input.";
    private const string Operation = "operationId";
    private const string Operator = "operatorReference";
    private const string Redirect = "redirectUri";
    private const string PublicKey = "publicKey";
    private static readonly string[] Members = [Operation, Operator, Redirect, PublicKey];

    /// <summary>Bounds UTF-8 input before JSON parsing and discards raw parser messages that could disclose submitted content.</summary>
    internal static async Task<AdministratorRegistrationInput> ReadAsync(Stream input, CancellationToken token)
    {
        var bytes = new byte[MaximumBytes + 1];
        try
        {
            var length = 0;
            while (length < bytes.Length)
            {
                var read = await input.ReadAsync(bytes.AsMemory(length), token);
                if (read == 0)
                {
                    break;
                }
                length += read;
            }
            if (length > MaximumBytes)
            {
                throw InvalidInput();
            }
            _ = new UTF8Encoding(false, true).GetCharCount(bytes, 0, length);
            using var document = JsonDocument.Parse(bytes.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = MaximumDepth });
            return Parse(document.RootElement) ?? throw InvalidInput();
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or InvalidOperationException)
        {
            throw InvalidInput();
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Requires exactly the declared public fields and an immutable policy before any database connection is opened.</summary>
    private static AdministratorRegistrationInput? Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != Members.Length ||
            !root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(Members) ||
            String(root, Operation) is not { } operation || !Guid.TryParseExact(operation, GuidFormat, out var id) || id == Guid.Empty ||
            String(root, Operator) is not { Length: > 0 and <= MaximumOperatorLength } actor ||
            !actor.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-') ||
            String(root, Redirect) is not { } redirect || root.GetProperty(PublicKey).ValueKind != JsonValueKind.Object ||
            AdministratorPublicKey.Parse(root.GetProperty(PublicKey).GetRawText()) is not { } key ||
            AdministratorRegistrationPolicy.Create(redirect, key) is not { } policy)
        {
            return null;
        }
        return new(id, actor, policy);
    }

    /// <summary>Reads only original scalar strings without coercing arrays, numbers or null.</summary>
    private static string? String(JsonElement root, string name) => root.GetProperty(name).ValueKind == JsonValueKind.String ? root.GetProperty(name).GetString() : null;

    /// <summary>Uses a fixed safe failure without the parser exception or submitted document.</summary>
    private static ArgumentException InvalidInput() => new(InvalidMessage);
}
