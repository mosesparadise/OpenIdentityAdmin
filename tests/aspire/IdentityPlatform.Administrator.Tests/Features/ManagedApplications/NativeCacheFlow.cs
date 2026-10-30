using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Builds genuine native client requests and independently reads committed manager state without exposing credentials in assertions.</summary>
internal static class NativeCacheFlow
{
    private const string JsonMediaType = "application/json";
    /// <summary>Creates complete initial authorization bytes with independent state, nonce and verifier values.</summary>
    internal static Dictionary<string, string> Initial(string verifier, string client = NativeCacheProfile.Client, string callback = NativeCacheProfile.Callback) => new(StringComparer.Ordinal)
    {
        [OpenIddictConstants.Parameters.ClientId] = client,
        [OpenIddictConstants.Parameters.RedirectUri] = callback,
        [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
        [OpenIddictConstants.Parameters.Scope] = OpenIddictConstants.Scopes.OpenId,
        [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
        [OpenIddictConstants.Parameters.CodeChallenge] = NativeCacheProfile.Challenge(verifier),
        [OpenIddictConstants.Parameters.State] = NativeCacheProfile.Random(),
        [OpenIddictConstants.Parameters.Nonce] = NativeCacheProfile.Random()
    };

    /// <summary>Requires an actual issuer self-redirect and extracts only its native query; an OAuth error or RP redirect cannot satisfy caching.</summary>
    internal static Dictionary<string, string> Resume(NativeCacheResponse response)
    {
        Assert.Equal(StatusCodes.Status302Found, response.Status);
        Assert.True(response.Handled);
        var location = new Uri(response.Location);
        Assert.True(location.GetLeftPart(UriPartial.Path) == new Uri(new Uri(NativeCacheProfile.Issuer), NativeCacheProfile.Authorize).AbsoluteUri);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.False(query.ContainsKey(OpenIddictConstants.Parameters.Error));
        Assert.True(query.TryGetValue(OpenIddictConstants.Parameters.RequestUri, out var reference) && reference.Count == 1);
        return query.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
    }

    /// <summary>Accepts only a genuine code response to the registered RP callback, without logging the credential or state.</summary>
    internal static string Code(NativeCacheResponse response)
    {
        Assert.Equal(StatusCodes.Status302Found, response.Status);
        var location = new Uri(response.Location);
        Assert.True(location.GetLeftPart(UriPartial.Path) == NativeCacheProfile.Callback);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.False(query.ContainsKey(OpenIddictConstants.Parameters.Error));
        Assert.True(query.TryGetValue(OpenIddictConstants.Parameters.Code, out var code) && code.Count == 1 && !string.IsNullOrEmpty(code[0]));
        return code[0]!;
    }

    /// <summary>Uses native client-secret Basic and actual verifier/code bytes for exchange.</summary>
    internal static Task<NativeCacheResponse> ExchangeAsync(NativeCacheFixture fixture, string code, string verifier) =>
        NativeCacheHttp.InvokeAsync(fixture, NativeCacheProfile.Token, new(StringComparer.Ordinal)
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.AuthorizationCode,
            [OpenIddictConstants.Parameters.RedirectUri] = NativeCacheProfile.Callback,
            [OpenIddictConstants.Parameters.Code] = code,
            [OpenIddictConstants.Parameters.CodeVerifier] = verifier
        });

    /// <summary>Resolves the actual protected request through native reference hashing in a new owned read scope.</summary>
    internal static Task<string> RequestIdAsync(NativeCacheFixture fixture, Dictionary<string, string> resume) => fixture.ReadAsync(async (scope, token) =>
    {
        var reference = resume[OpenIddictConstants.Parameters.RequestUri];
        var prefixed = reference.StartsWith(OpenIddictConstants.RequestUris.Prefixes.Generic, StringComparison.Ordinal);
        Assert.True(prefixed);
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.FindByReferenceIdAsync(reference[OpenIddictConstants.RequestUris.Prefixes.Generic.Length..], token);
        Assert.NotNull(row);
        return (await manager.GetIdAsync(row, token))!;
    });

    /// <summary>Reads committed status independently of the HTTP flow's tracked entity or response claims.</summary>
    internal static Task<string?> StatusAsync(NativeCacheFixture fixture, string id) => fixture.ReadAsync(async (scope, token) =>
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.FindByIdAsync(id, token);
        Assert.NotNull(row);
        return await manager.GetStatusAsync(row, token);
    });

    /// <summary>Counts all actual native tokens and authorizations, including valid, redeemed and rejected rows.</summary>
    internal static Task<(long Tokens, long Authorizations)> CountsAsync(NativeCacheFixture fixture) => fixture.ReadAsync(async (scope, token) =>
        (await scope.Services.GetRequiredService<IOpenIddictTokenManager>().CountAsync(token),
            await scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>().CountAsync(token)));

    /// <summary>Reads the sole native code identifier independently of its encrypted client representation.</summary>
    internal static Task<string> CodeIdAsync(NativeCacheFixture fixture) => fixture.ReadAsync((scope, token) =>
        scope.Owner.RunAsync(current => new ValueTask<string>(scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode).Select(row => row.Id!).SingleAsync(current)), token));

    /// <summary>Requires the scenario's exact native error, transport status and callback provenance without treating setup failures as replay evidence.</summary>
    internal static void RequireRejection(NativeCacheResponse response, string error, int status)
    {
        Assert.Equal(error, response.Error);
        Assert.Equal(status, response.Status);
        if (status == StatusCodes.Status302Found)
        {
            var location = new Uri(response.Location);
            Assert.True(location.GetLeftPart(UriPartial.Path) == NativeCacheProfile.Callback);
            var query = QueryHelpers.ParseQuery(location.Query);
            Assert.True(query.TryGetValue(OpenIddictConstants.Parameters.Error, out var value) && value.Count == 1 && value[0] == error);
            Assert.False(query.ContainsKey(OpenIddictConstants.Parameters.Code));
            return;
        }
        Assert.Empty(response.Location);
        Assert.NotEmpty(response.Body);
        if (response.ContentType?.StartsWith(JsonMediaType, StringComparison.Ordinal) == true)
        {
            using var document = JsonDocument.Parse(response.Body);
            Assert.Equal(error, document.RootElement.GetProperty(OpenIddictConstants.Parameters.Error).GetString());
            Assert.False(document.RootElement.TryGetProperty(OpenIddictConstants.Parameters.AccessToken, out _));
        }
    }

    /// <summary>Requires both native identity and access output without printing their values.</summary>
    internal static void RequireExchange(NativeCacheResponse response)
    {
        Assert.Equal(StatusCodes.Status200OK, response.Status);
        using var document = JsonDocument.Parse(response.Body);
        Assert.True(document.RootElement.TryGetProperty(OpenIddictConstants.Parameters.AccessToken, out var access) && !string.IsNullOrEmpty(access.GetString()));
        Assert.True(document.RootElement.TryGetProperty(OpenIddictConstants.Parameters.IdToken, out var identity) && !string.IsNullOrEmpty(identity.GetString()));
        Assert.False(document.RootElement.TryGetProperty(OpenIddictConstants.Parameters.RefreshToken, out _));
    }
}
