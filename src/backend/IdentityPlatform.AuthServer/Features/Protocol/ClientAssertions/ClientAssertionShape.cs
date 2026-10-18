using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;

/// <summary>Checks the bounded fixed assertion shape only after the installed native cryptographic validator accepted the same compact token.</summary>
internal static class ClientAssertionShape
{
    internal const int MaximumBytes = 8192;
    internal const int MinimumRsaBits = 3072;
    private const int MaximumJsonDepth = 4;
    private const string Algorithm = "alg";
    private const string Type = "typ";
    private const string KeyId = "kid";
    private static readonly string[] ForbiddenHeaders = ["jku", "x5u", "jwk", "x5c", "crit"];
    private static readonly string[] ReservedClaims = [OpenIddictConstants.Claims.Issuer, OpenIddictConstants.Claims.Subject,
        OpenIddictConstants.Claims.Audience, OpenIddictConstants.Claims.IssuedAt, OpenIddictConstants.Claims.ExpiresAt,
        OpenIddictConstants.Claims.NotBefore, OpenIddictConstants.Claims.JwtId];

    /// <summary>Rejects normalized duplicate claims and key fallback headers without implementing or replacing signature verification.</summary>
    internal static AdministratorAssertion? Read(OpenIddictServerEvents.ValidateTokenContext context, string issuer, string keyId)
    {
        if (context.IsRejected || context.TokenValidationResult is not { IsValid: true, SecurityToken: JsonWebToken token } ||
            token.InnerToken is not null || !string.Equals(token.EncodedToken, context.Token, StringComparison.Ordinal) ||
            token.SigningKey is not { } key || key.KeySize < MinimumRsaBits || !key.IsSupportedAlgorithm(SecurityAlgorithms.RsaSha256) ||
            !string.Equals(key.KeyId, keyId, StringComparison.Ordinal) ||
            context.Principal is null || !context.Principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion) ||
            !context.ValidTokenTypes.SetEquals([OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion]) ||
            string.IsNullOrEmpty(keyId) || context.Token.Length > MaximumBytes || !context.Token.All(char.IsAscii))
        {
            return null;
        }
        try
        {
            using var header = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedHeader), new JsonDocumentOptions { MaxDepth = MaximumJsonDepth });
            using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload), new JsonDocumentOptions { MaxDepth = MaximumJsonDepth });
            return HeaderMatches(header.RootElement, keyId) ? ReadPayload(payload.RootElement, issuer) : null;
        }
        catch (JsonException) { return null; }
        catch (FormatException) { return null; }
    }

    /// <summary>Requires exact fixed-key RS256 metadata and rejects embedded or remotely referenced key material.</summary>
    private static bool HeaderMatches(JsonElement header, string keyId) => UniqueObject(header) &&
        String(header, Algorithm) == SecurityAlgorithms.RsaSha256 && String(header, KeyId) == keyId &&
        String(header, Type) is OpenIddictConstants.JsonWebTokenTypes.ClientAuthentication or
            OpenIddictConstants.JsonWebTokenTypes.Prefixes.Application + OpenIddictConstants.JsonWebTokenTypes.ClientAuthentication &&
        !header.EnumerateObject().Any(property => ForbiddenHeaders.Contains(property.Name, StringComparer.OrdinalIgnoreCase));

    /// <summary>Preserves scalar integer and audience multiplicity before native claims accessors can normalize them.</summary>
    private static AdministratorAssertion? ReadPayload(JsonElement payload, string issuer)
    {
        if (!UniqueObject(payload) || payload.EnumerateObject().Any(property => ReservedClaims.Any(name =>
                string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase) && name != property.Name)) ||
            String(payload, OpenIddictConstants.Claims.Issuer) != AdministratorProtocolIdentity.ClientId ||
            String(payload, OpenIddictConstants.Claims.Subject) != AdministratorProtocolIdentity.ClientId || !AudienceMatches(payload, issuer) ||
            Integer(payload, OpenIddictConstants.Claims.IssuedAt) is not { } issued || Integer(payload, OpenIddictConstants.Claims.ExpiresAt) is not { } expires ||
            String(payload, OpenIddictConstants.Claims.JwtId) is not { } identifier)
        {
            return null;
        }
        var notBefore = Integer(payload, OpenIddictConstants.Claims.NotBefore);
        return payload.TryGetProperty(OpenIddictConstants.Claims.NotBefore, out _) && notBefore is null ? null :
            AdministratorAssertion.Create(identifier, issued, expires, notBefore);
    }

    /// <summary>Accepts the native single audience string or one-element array, always ordinally equal to the canonical issuer.</summary>
    private static bool AudienceMatches(JsonElement payload, string issuer)
    {
        if (!payload.TryGetProperty(OpenIddictConstants.Claims.Audience, out var audience))
        {
            return false;
        }
        return audience.ValueKind == JsonValueKind.String ? audience.GetString() == issuer :
            audience.ValueKind == JsonValueKind.Array && audience.GetArrayLength() == 1 &&
            audience[0].ValueKind == JsonValueKind.String && audience[0].GetString() == issuer;
    }

    /// <summary>Rejects duplicate or case-aliased JSON members at the original serialized boundary.</summary>
    private static bool UniqueObject(JsonElement element) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == element.EnumerateObject().Count();

    /// <summary>Reads only a real JSON string; arrays, objects, numbers and null never become scalar identity fields.</summary>
    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Reads only a nonnegative scalar integer representable in the persisted UTC-second range.</summary>
    private static long? Integer(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var integer) && integer >= 0 ? integer : null;
}
