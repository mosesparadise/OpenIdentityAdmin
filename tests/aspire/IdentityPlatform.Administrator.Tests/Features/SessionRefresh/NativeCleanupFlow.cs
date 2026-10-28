using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Drives actual native PKCE code/refresh issuance and independently observes its committed authorization family.</summary>
internal static class NativeCleanupFlow
{
    private const int ProofSeconds = 60;
    private const int ConfirmationFields = 2;
    private const string ScopeSeparator = " ";

    /// <summary>Retains only the pre-exchange intent and verifier alongside a genuinely cryptographically issued native code.</summary>
    internal static async Task<NativeCleanupCode> CodeAsync(NativeCleanupFixture fixture, string? intent = null)
    {
        intent ??= NativeCleanupProfile.Random();
        var verifier = NativeCleanupProfile.Random();
        var response = await NativeCleanupHttp.InvokeAsync(fixture, NativeCleanupProfile.Authorize, new()
        {
            [OpenIddictConstants.Parameters.ClientId] = NativeCleanupProfile.Client,
            [OpenIddictConstants.Parameters.RedirectUri] = NativeCleanupProfile.Callback,
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.Scope] = NativeCleanupProfile.Scope + ScopeSeparator + OpenIddictConstants.Scopes.OfflineAccess,
            [OpenIddictConstants.Parameters.CodeChallenge] = NativeCleanupProfile.Challenge(verifier),
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
            [NativeCleanupProfile.Intent] = intent
        });
        Assert.Equal(StatusCodes.Status302Found, response.Status);
        var query = QueryHelpers.ParseQuery(new Uri(response.Location).Query);
        Assert.False(query.ContainsKey(OpenIddictConstants.Parameters.Error));
        Assert.True(query.TryGetValue(OpenIddictConstants.Parameters.Code, out var code));
        Assert.Equal(1, code.Count);
        return new(intent, verifier, code[0]!);
    }

    /// <summary>Exchanges a real code through installed native private_key_jwt and PKCE validation.</summary>
    internal static Task<NativeCleanupResponse> ExchangeAsync(NativeCleanupFixture fixture, NativeCleanupCode code)
    {
        var fields = Proof(fixture);
        fields[OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.AuthorizationCode;
        fields[OpenIddictConstants.Parameters.Code] = code.Code;
        fields[OpenIddictConstants.Parameters.CodeVerifier] = code.Verifier;
        fields[OpenIddictConstants.Parameters.RedirectUri] = NativeCleanupProfile.Callback;
        return NativeCleanupHttp.InvokeAsync(fixture, NativeCleanupProfile.Token, fields);
    }

    /// <summary>Exercises actual refresh token cryptography and persisted native authorization state.</summary>
    internal static Task<NativeCleanupResponse> RefreshAsync(NativeCleanupFixture fixture, string refresh)
    {
        var fields = Proof(fixture);
        fields[OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.RefreshToken;
        fields[OpenIddictConstants.Parameters.RefreshToken] = refresh;
        return NativeCleanupHttp.InvokeAsync(fixture, NativeCleanupProfile.Token, fields);
    }

    /// <summary>Creates genuine signed synthetic proofs for negative request controls; successful cleanup uses the native client service.</summary>
    internal static Dictionary<string, string> Proof(NativeCleanupFixture fixture, SecurityKey? key = null, string? audience = null, int ageSeconds = 0, string client = NativeCleanupProfile.Client)
    {
        var now = DateTimeOffset.UtcNow.AddSeconds(-ageSeconds);
        var proof = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = client,
            Audience = audience ?? NativeCleanupProfile.Issuer,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = client,
                [JwtRegisteredClaimNames.Jti] = NativeCleanupProfile.Random()
            },
            IssuedAt = now.UtcDateTime,
            Expires = now.AddSeconds(ProofSeconds).UtcDateTime,
            TokenType = OpenIddictConstants.JsonWebTokenTypes.ClientAuthentication,
            SigningCredentials = new SigningCredentials(key ?? fixture.ClientSigning, SecurityAlgorithms.RsaSha256)
        });
        return new()
        {
            [OpenIddictConstants.Parameters.ClientId] = client,
            [OpenIddictConstants.Parameters.ClientAssertionType] = OpenIddictConstants.ClientAssertionTypes.JwtBearer,
            [OpenIddictConstants.Parameters.ClientAssertion] = proof
        };
    }

    /// <summary>Reads committed state in a fresh owned scope; no HTTP result or tracked entity is accepted as persistence evidence.</summary>
    internal static Task<NativeCleanupState?> StateAsync(NativeCleanupFixture fixture, string intent) => fixture.RunAsync(async (scope, token) =>
    {
        var tokens = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var selector = await tokens.FindByReferenceIdAsync(NativeCleanupProfile.Reference(NativeCleanupProfile.Client, intent), token);
        if (selector is null)
        {
            return null;
        }
        Assert.Null(await tokens.GetPayloadAsync(selector, token));
        var id = await tokens.GetAuthorizationIdAsync(selector, token);
        Assert.NotNull(id);
        var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var family = await authorizations.FindByIdAsync(id, token);
        Assert.NotNull(family);
        var states = new List<(string? Id, string? Type, string? Status)>();
        await foreach (var row in tokens.FindByAuthorizationIdAsync(id, token))
        {
            states.Add((await tokens.GetIdAsync(row, token), await tokens.GetTypeAsync(row, token), await tokens.GetStatusAsync(row, token)));
        }
        return new NativeCleanupState(id, await authorizations.GetStatusAsync(family, token), states.OrderBy(row => row.Id, StringComparer.Ordinal).ToList());
    });

    /// <summary>Extracts only an explicitly requested field from a synthetic successful response and never logs the response body.</summary>
    internal static string Field(NativeCleanupResponse response, string name)
    {
        Assert.Equal(StatusCodes.Status200OK, response.Status);
        using var json = JsonDocument.Parse(response.Body);
        Assert.False(json.RootElement.TryGetProperty(OpenIddictConstants.Parameters.Error, out _));
        Assert.True(json.RootElement.TryGetProperty(name, out var value));
        return value.GetString()!;
    }

    /// <summary>Requires both private fields and the original nonce; an ordinary empty RFC7009 response cannot confirm cleanup.</summary>
    internal static bool Confirmed(OpenIddictResponse response, string nonce) =>
        response.GetParameters().Count == ConfirmationFields && (bool?)response[NativeCleanupProfile.Confirmation] == true &&
        (string?)response[NativeCleanupProfile.Nonce] == nonce;

    /// <summary>Counts all native rows after disposal to detect orphan authorizations or codes after failed issuance.</summary>
    internal static Task<(long Authorizations, long Tokens)> CountsAsync(NativeCleanupFixture fixture) => fixture.RunAsync(async (scope, token) =>
        (await scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>().CountAsync(token),
            await scope.Services.GetRequiredService<IOpenIddictTokenManager>().CountAsync(token)));
}

/// <summary>Retains synthetic native grant material without generating a credential-bearing ToString method.</summary>
internal sealed class NativeCleanupCode(string intent, string verifier, string code)
{
    internal string Intent => intent;
    internal string Verifier => verifier;
    internal string Code => code;
}

/// <summary>Contains only independently read native identifiers, types and statuses.</summary>
internal sealed record NativeCleanupState(string Authorization, string? Status, List<(string? Id, string? Type, string? Status)> Tokens);
