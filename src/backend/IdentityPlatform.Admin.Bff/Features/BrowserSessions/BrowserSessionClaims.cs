using System.Globalization;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Checks original signed payload shapes that native principal materialization can normalize; never verifies a token.</summary>
internal static class BrowserSessionClaims
{
    internal const string Subject = "sub";
    internal const string Session = "sid";
    internal const string SourceExpiry = "admin_session_expires_at";
    internal const string Intent = "admin_login_intent";
    internal const string AuthenticationTime = "auth_time";
    internal const string Methods = "amr";
    internal const string PasswordMethod = "pwd";
    internal const string FactorMethod = "otp";
    internal const int MethodCount = 2;
    private const int MaximumJsonDepth = 8;
    private static readonly string[] Required = [Subject, Session, SourceExpiry, Intent, AuthenticationTime, Methods];

    /// <summary>Inspects only the exact compact ID token already accepted by native cryptography, issuer/audience/nonce and callback checks.</summary>
    internal static Dictionary<string, JsonElement>? Read(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > SessionTicketLimits.NativeBytes)
        {
            return null;
        }
        try
        {
            var native = new JsonWebToken(token);
            if (native.IsEncrypted || string.IsNullOrEmpty(native.EncodedPayload))
            {
                return null;
            }
            using var document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(native.EncodedPayload),
                new JsonDocumentOptions { MaxDepth = MaximumJsonDepth });
            return Collect(document.RootElement);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Enumerates original properties so identical duplicates and case aliases cannot disappear in a dictionary/parser projection.</summary>
    private static Dictionary<string, JsonElement>? Collect(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in payload.EnumerateObject())
        {
            var name = Required.FirstOrDefault(value => string.Equals(value, property.Name, StringComparison.OrdinalIgnoreCase));
            if (name is not null && (property.Name != name || !result.TryAdd(name, property.Value.Clone())))
            {
                return null;
            }
        }
        return result.Count == Required.Length ? result : null;
    }

    /// <summary>Accepts one canonical nonnegative integral epoch second, excluding string, fraction, exponent and overflow representations.</summary>
    internal static long? Integer(JsonElement value) => value.ValueKind == JsonValueKind.Number &&
        long.TryParse(value.GetRawText(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) &&
        number <= DateTimeOffset.MaxValue.ToUnixTimeSeconds() &&
        value.GetRawText() == number.ToString(CultureInfo.InvariantCulture) ? number : null;

    /// <summary>Rejects non-scalar or oversized identity values without normalization.</summary>
    internal static string? Binding(JsonElement value) => value.ValueKind == JsonValueKind.String &&
        value.GetString() is { Length: > 0 and <= SessionTicketLimits.BindingLength } text ? text : null;

    /// <summary>Requires exactly the two native full-authentication methods; duplicate and nested representations deny.</summary>
    internal static bool FullMethods(JsonElement value) => value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == MethodCount &&
        value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String) &&
        value.EnumerateArray().Select(item => item.GetString()).ToHashSet(StringComparer.Ordinal).SetEquals([PasswordMethod, FactorMethod]);

    /// <summary>Accepts only the canonical uppercase SHA-256 digest supplied by protected server-side intent state.</summary>
    internal static bool CanonicalIntent(string? value) => value is { Length: SessionTicketLimits.DigestLength } &&
        value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
}
