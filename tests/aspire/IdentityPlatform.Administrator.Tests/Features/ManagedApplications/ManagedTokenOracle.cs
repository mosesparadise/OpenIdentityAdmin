using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Validates actual managed credentials against published signing keys and explicit identity-only claim contracts.</summary>
internal static class ManagedTokenOracle
{
    private const string DiscoveryPath = "/.well-known/openid-configuration";
    private const string JwksUri = "jwks_uri";
    private const string IdentityType = "JWT";
    private const string AccessType = "at+jwt";
    private const string IdentityField = "id_token";
    private const string AccessField = "access_token";
    private const string Subject = "sub";
    private const string Nonce = "nonce";
    private const string AccessHash = "at_hash";
    private const string ClientId = "client_id";
    private const string Scope = "scope";
    private const string OpenId = "openid";
    private const string AuthorizedParty = "azp";
    private const string Presenter = "oi_prst";
    private const string TokenId = "oi_tkn_id";
    private const string AuthorizationId = "oi_au_id";
    private const string StoredIdentityType = "urn:ietf:params:oauth:token-type:id_token";
    private const string StoredAccessType = "urn:ietf:params:oauth:token-type:access_token";
    private const int HalfDigestBytes = 16;
    private const string IssuedAt = "iat";
    private const string ExpiresAt = "exp";
    private const string Valid = "valid";
    private static readonly string[] IdentityClaims = ["iss", Subject, "aud", AuthorizedParty, "iat", "exp", "nbf", "jti", Nonce, AccessHash, AuthorizationId, TokenId];
    private static readonly string[] AccessClaims = ["iss", Subject, "iat", "exp", "nbf", "jti", ClientId, Scope, AuthorizationId, TokenId, Presenter];

    /// <summary>Checks signatures, lifetime, issuer, ID-token recipient and nonce/hash bindings without printing credential contents.</summary>
    internal static async Task ValidateAsync(ManagedCacheTestContext test, ManagedTokenFlow flow, JsonElement body, string subject)
    {
        using var discovery = JsonDocument.Parse(await test.Http.IssuerClient.GetStringAsync(DiscoveryPath, TestContext.Current.CancellationToken));
        var uri = new Uri(discovery.RootElement.GetProperty(JwksUri).GetString()!);
        Assert.True(uri.GetLeftPart(UriPartial.Authority) == test.Http.Environment.Profile.Issuer.GetLeftPart(UriPartial.Authority));
        var keys = new JsonWebKeySet(await test.Http.IssuerClient.GetStringAsync(uri, TestContext.Current.CancellationToken));
        Assert.All(keys.Keys, key => Assert.True(string.IsNullOrEmpty(key.D) && string.IsNullOrEmpty(key.K)));
        var identityParameters = Parameters(test, keys, IdentityType);
        identityParameters.ValidAudience = ManagedCacheTestContext.Client;
        var identity = await ValidateAsync(body.GetProperty(IdentityField).GetString()!, identityParameters, IdentityClaims);
        var access = body.GetProperty(AccessField).GetString()!;
        var expectedHash = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(access)).AsSpan(0, HalfDigestBytes).ToArray());
        Assert.True(identity.GetClaim(Subject) == subject && identity.GetClaim(Nonce) == flow.Nonce && identity.GetClaim(AccessHash) == expectedHash);
        var accessParameters = Parameters(test, keys, AccessType);
        accessParameters.ValidateAudience = false; // The explicit public claim allowlist below rejects every audience claim.
        var credentials = test.Http.Environment.IssuerServices.GetRequiredService<IssuerCredentials>();
        accessParameters.TokenDecryptionKeys = credentials.EncryptionKeys.All.Select(certificate => new X509SecurityKey(certificate));
        var principal = await ValidateAsync(access, accessParameters, AccessClaims);
        Assert.True(principal.GetClaim(Subject) == subject && principal.GetClaim(ClientId) == ManagedCacheTestContext.Client && principal.GetClaim(Scope) == OpenId);
        Assert.True(identity.GetClaim(AuthorizedParty) == ManagedCacheTestContext.Client && principal.GetClaim(Presenter) == ManagedCacheTestContext.Client);
        var snapshot = await test.SnapshotAsync();
        Assert.True(identity.GetClaim(TokenId) == snapshot.Tokens.Single(value => value.Type == StoredIdentityType).Id);
        Assert.True(principal.GetClaim(TokenId) == snapshot.Tokens.Single(value => value.Type == StoredAccessType).Id);
        var authorization = Assert.Single(snapshot.Interactions).AuthorizationId;
        Assert.True(identity.GetClaim(AuthorizationId) == authorization && principal.GetClaim(AuthorizationId) == authorization);
        await RequireLifetimeAsync(test, identity, snapshot);
        await RequireLifetimeAsync(test, principal, snapshot);
    }

    /// <summary>Binds public cryptographically validated timestamps to actual native rows and the original human and credential expiry.</summary>
    private static async Task RequireLifetimeAsync(ManagedCacheTestContext test, ClaimsPrincipal principal, ManagedCacheSnapshot snapshot)
    {
        var row = snapshot.Tokens.Single(value => value.Id == principal.GetClaim(TokenId));
        var created = long.Parse(principal.GetClaim(IssuedAt)!, CultureInfo.InvariantCulture);
        var expires = long.Parse(principal.GetClaim(ExpiresAt)!, CultureInfo.InvariantCulture);
        Assert.True(row.Status == Valid && row.CreationDate is { } start && new DateTimeOffset(start).ToUnixTimeSeconds() == created &&
            row.ExpirationDate is { } end && new DateTimeOffset(end).ToUnixTimeSeconds() == expires);
        Assert.True(expires <= Assert.Single(snapshot.Interactions).ExpiresAt);
        await using var scope = test.Http.Scope();
        var policy = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationPolicy>()
            .AsNoTracking().SingleAsync(value => value.ApplicationId == test.Id, TestContext.Current.CancellationToken);
        Assert.True(expires <= policy.CredentialExpiresAt);
    }

    /// <summary>Requires native signed credentials with exact issuer/type and strict current expiry independently of server response checks.</summary>
    private static TokenValidationParameters Parameters(ManagedCacheTestContext test, JsonWebKeySet keys, string type) => new()
    {
        IssuerSigningKeys = keys.GetSigningKeys(),
        ValidIssuer = test.Http.Environment.Profile.Issuer.AbsoluteUri,
        ValidTypes = [type],
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    /// <summary>Rejects extra public identity/authority claims on the cryptographically verified payload, without echoing their values.</summary>
    private static async Task<ClaimsPrincipal> ValidateAsync(string token, TokenValidationParameters parameters, string[] allowed)
    {
        var result = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(token, parameters);
        Assert.True(result.IsValid, result.Exception?.GetType().FullName);
        var principal = new ClaimsPrincipal(result.ClaimsIdentity);
        Assert.True(principal.Claims.All(claim => allowed.Contains(claim.Type, StringComparer.Ordinal)));
        Assert.True(principal.Claims.GroupBy(claim => claim.Type).All(group => group.Count() == 1));
        return principal;
    }
}
