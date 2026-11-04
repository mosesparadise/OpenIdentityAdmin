using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Retains full native access-token decryption and persisted lifetime/binding checks after shared public ID-token verification.</summary>
internal static class ManagedTokenOracle
{
    private const string AccessType = "at+jwt";
    private const string AccessField = "access_token";
    private const string Subject = "sub";
    private const string ClientId = "client_id";
    private const string Scope = "scope";
    private const string OpenId = "openid";
    private const string Presenter = "oi_prst";
    private const string TokenId = "oi_tkn_id";
    private const string AuthorizationId = "oi_au_id";
    private const string StoredIdentityType = "urn:ietf:params:oauth:token-type:id_token";
    private const string StoredAccessType = "urn:ietf:params:oauth:token-type:access_token";
    private const string IssuedAt = "iat";
    private const string ExpiresAt = "exp";
    private const string Valid = "valid";
    private static readonly string[] AccessClaims = ["iss", Subject, "iat", "exp", "nbf", "jti", ClientId, Scope, AuthorizationId, TokenId, Presenter];


    /// <summary>Retains the original native caller contract while borrowing its already-created registration and flow bindings.</summary>
    internal static Task ValidateAsync(ManagedCacheTestContext test, ManagedTokenFlow flow, JsonElement body, string subject, bool applicationOnly = false, bool multipleFlows = false) =>
        ValidateAsync(ManagedTokenEvidenceContext.Borrow(test), flow.Nonce, flow.RequestId, body, subject, applicationOnly, multipleFlows);

    /// <summary>Checks signatures, lifetime, issuer, ID-token recipient and nonce/hash bindings without printing credential contents.</summary>
    internal static async Task ValidateAsync(ManagedTokenEvidenceContext test, string nonce, string requestId, JsonElement body, string subject, bool applicationOnly = false, bool multipleFlows = false)
    {
        var verified = await ManagedIdentityTokenOracle.ValidateAsync(test.Transport, test.Environment.Profile.Issuer,
            test.ClientIdentifier, nonce, subject, body);
        var identity = verified.Principal;
        var keys = verified.SigningKeys;
        var access = body.GetProperty(AccessField).GetString()!;
        var accessParameters = ManagedTokenValidation.Parameters(test.Environment.Profile.Issuer, keys, AccessType);
        accessParameters.ValidateAudience = false; // The explicit public claim allowlist below rejects every audience claim.
        var credentials = test.Environment.IssuerServices.GetRequiredService<IssuerCredentials>();
        accessParameters.TokenDecryptionKeys = credentials.EncryptionKeys.All.Select(certificate => new X509SecurityKey(certificate));
        var principal = await ManagedTokenValidation.ValidateAsync(access, accessParameters, AccessClaims);
        Assert.True(principal.GetClaim(Subject) == subject && principal.GetClaim(ClientId) == test.ClientIdentifier && principal.GetClaim(Scope) == OpenId);
        Assert.True(principal.GetClaim(Presenter) == test.ClientIdentifier);
        var all = await test.SnapshotAsync(applicationOnly);
        var interaction = Assert.Single(all.Interactions, row => row.RequestId == requestId);
        var snapshot = multipleFlows
            ? new ManagedCacheSnapshot([interaction], all.Tokens, all.Authorizations, all.Sessions)
            : all;
        Assert.True(identity.GetClaim(TokenId) == snapshot.Tokens.Single(value => value.Type == StoredIdentityType).Id);
        Assert.True(principal.GetClaim(TokenId) == snapshot.Tokens.Single(value => value.Type == StoredAccessType).Id);
        var authorization = Assert.Single(snapshot.Interactions).AuthorizationId;
        Assert.True(identity.GetClaim(AuthorizationId) == authorization && principal.GetClaim(AuthorizationId) == authorization);
        await RequireLifetimeAsync(test, identity, snapshot);
        await RequireLifetimeAsync(test, principal, snapshot);
    }

    /// <summary>Binds public cryptographically validated timestamps to actual native rows and the original human and credential expiry.</summary>
    private static async Task RequireLifetimeAsync(ManagedTokenEvidenceContext test, ClaimsPrincipal principal, ManagedCacheSnapshot snapshot)
    {
        var row = snapshot.Tokens.Single(value => value.Id == principal.GetClaim(TokenId));
        var created = long.Parse(principal.GetClaim(IssuedAt)!, CultureInfo.InvariantCulture);
        var expires = long.Parse(principal.GetClaim(ExpiresAt)!, CultureInfo.InvariantCulture);
        Assert.True(row.Status == Valid && row.CreationDate is { } start && new DateTimeOffset(start).ToUnixTimeSeconds() == created &&
            row.ExpirationDate is { } end && new DateTimeOffset(end).ToUnixTimeSeconds() == expires);
        Assert.True(expires <= Assert.Single(snapshot.Interactions).ExpiresAt);
        await using var scope = test.Scope();
        var policy = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationPolicy>()
            .AsNoTracking().SingleAsync(value => value.ApplicationId == test.Id, TestContext.Current.CancellationToken);
        Assert.True(expires <= policy.CredentialExpiresAt);
    }

}
