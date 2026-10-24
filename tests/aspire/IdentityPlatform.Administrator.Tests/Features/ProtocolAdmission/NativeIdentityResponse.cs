using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Changes one signed identity-token field after actual issuance while proving signature validity and preserving the rest of the response.</summary>
internal static class NativeIdentityResponse
{
    internal enum Fault
    {
        Unchanged, Nonce, Issuer, Audience, Type, Expiry
    }
    private const string BrowserIntent = "admin_login_intent";
    private const string ChangedBrowserIntent = "0000000000000000000000000000000000000000000000000000000000000000";
    private const string ChangedNonce = "synthetic-wrong-response-nonce";
    private const string ChangedIssuer = "https://untrusted.example.test/";
    private const string ChangedAudience = "synthetic-other-recipient";
    private const string ChangedType = "at+jwt";
    private const int CompactParts = 3;
    private const int PayloadPart = 1;
    private const int SignaturePart = 2;
    private const string CompactSeparator = ".";

    /// <summary>Requires a real successful response; exact structural comparisons and public-key checks distinguish the selected fault from incidental corruption.</summary>
    internal static byte[] Rewrite(byte[] body, X509Certificate2 certificate, Fault fault, DateTimeOffset now, bool browserIntent = false)
    {
        var original = JsonNode.Parse(body)!.AsObject();
        var response = original.DeepClone().AsObject();
        var token = original[OpenIddictConstants.Parameters.IdToken]!.GetValue<string>();
        var parts = token.Split(CompactSeparator);
        Assert.Equal(CompactParts, parts.Length);
        RequireSignature(parts, certificate);
        var header = JsonNode.Parse(Base64UrlEncoder.Decode(parts[0]))!.AsObject();
        var payload = JsonNode.Parse(Base64UrlEncoder.Decode(parts[PayloadPart]))!.AsObject();
        Assert.True(header[JwtHeaderParameterNames.Alg]!.GetValue<string>() == SecurityAlgorithms.RsaSha256);
        var changed = payload.DeepClone().AsObject();
        var claim = browserIntent ? ChangeBrowserIntent(changed) : Change(changed, fault, now);
        RequireChange(payload, changed, claim);
        var type = fault == Fault.Type ? ChangedType : header[JwtHeaderParameterNames.Typ]!.GetValue<string>();
        var key = new X509SecurityKey(certificate) { KeyId = header[JwtHeaderParameterNames.Kid]!.GetValue<string>() };
        var signed = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(changed.ToJsonString(),
            new SigningCredentials(key, SecurityAlgorithms.RsaSha256), Headers(header, type));
        var replacement = signed.Split(CompactSeparator);
        RequireSignature(replacement, certificate);
        RequireChange(header, JsonNode.Parse(Base64UrlEncoder.Decode(replacement[0]))!.AsObject(), fault == Fault.Type ? JwtHeaderParameterNames.Typ : null);
        Assert.True(JsonNode.DeepEquals(changed, JsonNode.Parse(Base64UrlEncoder.Decode(replacement[PayloadPart]))));
        response[OpenIddictConstants.Parameters.IdToken] = signed;
        RequireOuter(original, response);
        return Encoding.UTF8.GetBytes(response.ToJsonString());
    }

    /// <summary>Re-signs exactly one canonical but incorrect B3 digest while retaining every native nonce, audience, timestamp and signature constraint.</summary>
    private static string ChangeBrowserIntent(JsonObject payload)
    {
        Assert.True(payload.ContainsKey(BrowserIntent));
        Assert.NotEqual(ChangedBrowserIntent, payload[BrowserIntent]!.GetValue<string>());
        payload[BrowserIntent] = ChangedBrowserIntent;
        return BrowserIntent;
    }

    /// <summary>Changes exactly one selected payload field; expiry equality remains later than issuance.</summary>
    private static string? Change(JsonObject payload, Fault fault, DateTimeOffset now)
    {
        var claim = fault switch
        {
            Fault.Nonce => OpenIddictConstants.Claims.Nonce,
            Fault.Issuer => OpenIddictConstants.Claims.Issuer,
            Fault.Audience => OpenIddictConstants.Claims.Audience,
            Fault.Expiry => OpenIddictConstants.Claims.ExpiresAt,
            _ => null
        };
        if (claim is null)
        {
            return null;
        }
        Assert.True(payload.ContainsKey(claim));
        if (fault == Fault.Expiry)
        {
            Assert.True(now.ToUnixTimeSeconds() > payload[OpenIddictConstants.Claims.IssuedAt]!.GetValue<long>());
            Assert.True(now.ToUnixTimeSeconds() < payload[claim]!.GetValue<long>());
            payload[claim] = now.ToUnixTimeSeconds();
        }
        else
        {
            payload[claim] = fault == Fault.Nonce ? ChangedNonce : fault == Fault.Issuer ? ChangedIssuer : ChangedAudience;
        }
        return claim;
    }

    /// <summary>Preserves original additional JOSE fields; signing credentials reproduce the reserved algorithm/key headers.</summary>
    private static Dictionary<string, object> Headers(JsonObject header, string type)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var pair in header)
        {
            if (pair.Key is not (JwtHeaderParameterNames.Alg or JwtHeaderParameterNames.Kid or JwtHeaderParameterNames.X5t))
            {
                result.Add(pair.Key, pair.Key == JwtHeaderParameterNames.Typ ? type : pair.Value!.GetValue<string>());
            }
        }
        return result;
    }

    /// <summary>Requires precisely the designated structural difference, reporting no claim or header values on failure.</summary>
    private static void RequireChange(JsonObject before, JsonObject after, string? changed)
    {
        Assert.Equal(before.Count, after.Count);
        foreach (var pair in before)
        {
            Assert.True(after.ContainsKey(pair.Key));
            Assert.Equal(pair.Key != changed, JsonNode.DeepEquals(pair.Value, after[pair.Key]));
        }
    }

    /// <summary>Verifies actual RS256 bytes independently of token-library parsing or the client's validation outcome.</summary>
    private static void RequireSignature(string[] parts, X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPublicKey();
        Assert.NotNull(rsa);
        Assert.True(rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + CompactSeparator + parts[PayloadPart]),
            Base64UrlEncoder.DecodeBytes(parts[SignaturePart]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    /// <summary>Leaves the access token and every other actual response value unchanged.</summary>
    private static void RequireOuter(JsonObject before, JsonObject after)
    {
        Assert.Equal(before.Count, after.Count);
        foreach (var pair in before.Where(pair => pair.Key != OpenIddictConstants.Parameters.IdToken))
        {
            Assert.True(after.ContainsKey(pair.Key) && JsonNode.DeepEquals(pair.Value, after[pair.Key]));
        }
    }
}
