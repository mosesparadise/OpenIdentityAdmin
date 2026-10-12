using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IdentityPlatform.Migrator.Features.Bootstrap.Initialize;

// Deliberately not a record: generated ToString must never expose the password.
public sealed class BootstrapInput
{
    private BootstrapInput(Guid operationId, string operatorReference, string userName, string password)
    {
        OperationId = operationId;
        OperatorReference = operatorReference;
        UserName = userName;
        Password = password;
    }

    public Guid OperationId
    {
        get;
    }
    public string OperatorReference
    {
        get;
    }
    public string UserName
    {
        get;
    }
    public string Password
    {
        get;
    }

    public static async Task<BootstrapInput> ReadAsync(Stream input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var bytes = new byte[4097];
        try
        {
            var length = 0;
            while (length < bytes.Length)
            {
                var count = await input.ReadAsync(bytes.AsMemory(length), cancellationToken);
                if (count == 0)
                {
                    break;
                }
                length += count;
            }
            if (length > 4096)
            {
                throw InvalidInput();
            }
            // JsonDocument may defer string decoding; reject malformed UTF-8 before inspection.
            _ = new UTF8Encoding(false, true).GetCharCount(bytes, 0, length);
            using var document = JsonDocument.Parse(bytes.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 2 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw InvalidInput();
            }
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name is not ("operationId" or "operatorReference" or "userName" or "password") ||
                    property.Value.ValueKind != JsonValueKind.String || !fields.TryAdd(property.Name, property.Value.GetString()!))
                {
                    throw InvalidInput();
                }
            }
            if (fields.Count != 4 || !Guid.TryParseExact(fields["operationId"], "D", out var operationId) || operationId == Guid.Empty ||
                !ValidName(fields["operatorReference"], 64, false) || !ValidName(fields["userName"], 128, true) ||
                fields["password"].Length is < 16 or > 256)
            {
                throw InvalidInput();
            }
            return new BootstrapInput(operationId, fields["operatorReference"], fields["userName"], fields["password"]);
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or InvalidOperationException)
        {
            // Do not attach parser exceptions: their messages can contain submitted content.
            throw InvalidInput();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static bool ValidName(string value, int maximum, bool user) => value.Length >= 1 && value.Length <= maximum &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' || (user && character is '@' or '+'));

    private static ArgumentException InvalidInput() => new("Invalid administrator bootstrap input.");
}
