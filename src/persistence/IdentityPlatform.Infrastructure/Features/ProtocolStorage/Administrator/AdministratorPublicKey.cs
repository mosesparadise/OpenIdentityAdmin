using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Snapshots one explicit public RSA verification key without retaining mutable or private key material.</summary>
internal sealed class AdministratorPublicKey
{
    internal const int MinimumBits = 3072;
    private const int MaximumDocumentLength = 8192;
    private const int MaximumIdentifierLength = 128;
    private const string RsaType = "RSA";
    private const string SignatureUse = "sig";
    private static readonly string[] Members = ["kty", "kid", "alg", "use", "n", "e"];
    private readonly string json;
    internal string KeyId
    {
        get;
    }
    internal string Fingerprint
    {
        get;
    }

    /// <summary>Retains a canonical public-only encoding whose digest also binds the key identifier and algorithm.</summary>
    private AdministratorPublicKey(JsonWebKey key)
    {
        KeyId = key.Kid;
        json = JsonSerializer.Serialize(new
        {
            kty = key.Kty,
            kid = key.Kid,
            alg = key.Alg,
            use = key.Use,
            n = key.N,
            e = key.E
        });
        Fingerprint = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>Accepts only the fixed public JWK shape and canonical unsigned integers before native RSA import validates the key.</summary>
    internal static AdministratorPublicKey? Parse(string json)
    {
        if (json.Length > MaximumDocumentLength)
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != Members.Length ||
                !root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(Members) ||
                root.EnumerateObject().Any(property => property.Value.ValueKind != JsonValueKind.String))
            {
                return null;
            }
            var key = new JsonWebKey(json);
            if (key.Kty != RsaType || key.Alg != SecurityAlgorithms.RsaSha256 || key.Use != SignatureUse ||
                key.Kid is not { Length: > 0 and <= MaximumIdentifierLength } || !key.Kid.All(char.IsAscii) || key.Kid.Any(char.IsControl) ||
                !TryInteger(key.N, out var modulus) || !TryInteger(key.E, out var exponent))
            {
                return null;
            }
            using var rsa = RSA.Create(new RSAParameters { Modulus = modulus, Exponent = exponent });
            return rsa.KeySize >= MinimumBits ? new(key) : null;
        }
        catch (JsonException) { return null; }
        catch (ArgumentException) { return null; }
        catch (CryptographicException) { return null; }
    }

    /// <summary>Returns an independent native descriptor key so callers cannot mutate this expected profile.</summary>
    internal JsonWebKey CreateNativeKey() => new(json);

    /// <summary>Requires exactly the approved public metadata, rejecting extra private, embedded or remotely referenced key fields.</summary>
    internal bool Matches(JsonWebKey key)
    {
        var expected = CreateNativeKey();
        return key.Kty == expected.Kty && key.Kid == expected.Kid && key.Alg == expected.Alg && key.Use == expected.Use &&
            key.N == expected.N && key.E == expected.E && key.AdditionalData.Count == 0 && key.KeyOps.Count == 0 && key.X5c.Count == 0 && key.Oth.Count == 0 &&
            key.D is null && key.DP is null && key.DQ is null && key.P is null && key.Q is null && key.QI is null && key.K is null &&
            key.Crv is null && key.X is null && key.Y is null && key.X5u is null && key.X5t is null && key.X5tS256 is null && key.Pub is null && key.Priv is null;
    }

    /// <summary>Binds the real validated signing key to the expected public RSA material, independently of an attacker-controlled header kid.</summary>
    internal bool MatchesValidatedKey(SecurityKey key) => key.KeyId == KeyId && key.CanComputeJwkThumbprint() &&
        CryptographicOperations.FixedTimeEquals(key.ComputeJwkThumbprint(), CreateNativeKey().ComputeJwkThumbprint());

    /// <summary>Rejects padding, alternative encodings, empty integers and redundant unsigned leading zero octets.</summary>
    private static bool TryInteger(string value, out byte[] decoded)
    {
        decoded = [];
        try
        {
            decoded = Base64UrlEncoder.DecodeBytes(value);
            return decoded.Length != 0 && decoded[0] != 0 && Base64UrlEncoder.Encode(decoded) == value;
        }
        catch (FormatException) { return false; }
    }
}
