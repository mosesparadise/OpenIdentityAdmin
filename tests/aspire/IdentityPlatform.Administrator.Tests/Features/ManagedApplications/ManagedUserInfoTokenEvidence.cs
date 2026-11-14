using System.Text.Json.Nodes;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises issuer-signed bearer binding and nonhuman negatives without enabling another grant or bypassing native validation.</summary>
internal static class ManagedUserInfoTokenEvidence
{
    private const string AccessType = "at+jwt";
    private const string Subject = "sub";
    private const string Expiration = "exp";
    private const string Issued = "iat";
    private const string Changed = "different-native-binding";
    private static readonly string[] Bindings = ["client_id", "oi_prst", Subject, Expiration];

    /// <summary>Requires a re-signed unchanged positive control before independently signed mismatches, expiry and absent-human-subject cases.</summary>
    internal static async Task RequireAsync(ManagedCacheTestContext test, string access, string subject)
    {
        var credentials = test.Http.Environment.IssuerServices.GetRequiredService<IssuerCredentials>();
        var payload = await PayloadAsync(test, access, credentials);
        var before = await ManagedUserInfoEvidence.CaptureAsync(test);
        using var control = await ManagedConsentTest.ReadAsync(test, await SignAsync(test, payload, credentials));
        await ManagedConsentTest.RequireAsync(control, subject, null, false);
        await before.RequireAsync(test, commits: 1);
        var negativeState = await ManagedUserInfoEvidence.CaptureAsync(test);
        foreach (var binding in Bindings)
        {
            var changed = payload.DeepClone().AsObject();
            Assert.True(changed.ContainsKey(binding));
            changed[binding] = binding == Expiration ? JsonValue.Create(payload[Issued]!.GetValue<long>()) : JsonValue.Create(Changed);
            await DeniedAsync(test, changed, credentials);
        }
        var nonhuman = payload.DeepClone().AsObject();
        Assert.True(nonhuman.Remove(Subject));
        await DeniedAsync(test, nonhuman, credentials);
        await negativeState.RequireAsync(test, commits: 0);
    }

    /// <summary>Checks the real native denial after independently verifying signature and the exact intended replacement payload.</summary>
    private static async Task DeniedAsync(ManagedCacheTestContext test, JsonObject payload, IssuerCredentials credentials)
    {
        using var denied = await ManagedConsentTest.ReadAsync(test, await SignAsync(test, payload, credentials));
        await ManagedConsentTest.DeniedAsync(denied);
    }

    /// <summary>Uses genuine test-issuer keys and the existing access-token type; no token or authorization entry is created or changed.</summary>
    private static async Task<string> SignAsync(ManagedCacheTestContext test, JsonObject payload, IssuerCredentials credentials)
    {
        var signed = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(payload.ToJsonString(),
            new SigningCredentials(new X509SecurityKey(credentials.Signing), SecurityAlgorithms.RsaSha256),
            new Dictionary<string, object> { [JwtHeaderParameterNames.Typ] = AccessType });
        Assert.True(JsonNode.DeepEquals(payload, await PayloadAsync(test, signed, credentials)));
        return signed;
    }

    /// <summary>Validates actual signed access-token bytes independently; lifetime is inspected by the real native endpoint in negative cases.</summary>
    private static async Task<JsonObject> PayloadAsync(ManagedCacheTestContext test, string access, IssuerCredentials credentials)
    {
        var result = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(access, new TokenValidationParameters
        {
            IssuerSigningKeys = credentials.SigningKeys.All.Select(certificate => new X509SecurityKey(certificate)),
            ValidIssuer = test.Http.Environment.Profile.Issuer.AbsoluteUri,
            ValidTypes = [AccessType],
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = false
        });
        Assert.True(result.IsValid, result.Exception?.GetType().FullName);
        var token = Assert.IsType<JsonWebToken>(result.SecurityToken);
        Assert.Null(token.InnerToken);
        return JsonNode.Parse(Base64UrlEncoder.Decode(token.EncodedPayload))!.AsObject();
    }
}
