using System.Text.Json;

namespace IdentityPlatform.Sample.Client.Features.Configuration;

/// <summary>Retains the explicitly entered client credential without a record-generated secret-bearing string representation.</summary>
internal sealed class SampleSettings
{
    private const int ConfigurationFieldCount = 2;
    private const string ClientIdField = "clientId";
    private const string ClientSecretField = "clientSecret";

    internal const int InputLimit = 4096;
    private const int ClientIdLimit = 100;
    private const int SecretLimit = 256;
    internal string ClientId
    {
        get;
    }
    internal string ClientSecret
    {
        get;
    }

    /// <summary>Constructs only from bounded validated setup input.</summary>
    private SampleSettings(string clientId, string secret)
    {
        ClientId = clientId;
        ClientSecret = secret;
    }

    /// <summary>Rejects duplicate/unknown properties and malformed input without echoing any supplied material.</summary>
    internal static SampleSettings Parse(ReadOnlyMemory<byte> input)
    {
        if (input.Length is 0 or > InputLimit)
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        try
        {
            using var document = JsonDocument.Parse(input, new JsonDocumentOptions { MaxDepth = ConfigurationFieldCount });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != ConfigurationFieldCount ||
                !root.TryGetProperty(ClientIdField, out var identifier) || !root.TryGetProperty(ClientSecretField, out var credential) ||
                identifier.ValueKind != JsonValueKind.String || credential.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(SampleProfile.Failure);
            }
            var clientId = identifier.GetString()!;
            var secret = credential.GetString()!;
            if (clientId.Length is 0 or > ClientIdLimit || !clientId.All(value => char.IsAsciiLetterOrDigit(value) || value is '-' or '_' or '.') ||
                secret.Length is 0 or > SecretLimit || secret.Any(char.IsControl))
            {
                throw new InvalidOperationException(SampleProfile.Failure);
            }
            return new SampleSettings(clientId, secret);
        }
        catch (JsonException) { throw new InvalidOperationException(SampleProfile.Failure); }
    }
}
