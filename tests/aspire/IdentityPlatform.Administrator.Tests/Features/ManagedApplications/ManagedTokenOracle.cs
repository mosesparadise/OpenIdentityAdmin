using System.Globalization;
using System.Security.Claims;
using System.Text;
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
    private const string Audience = "aud";

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
    private static readonly CompositeFormat Cardinality = CompositeFormat.Parse("Current authorization token counts: identity={0}, access={1}; all tokens={2}; all authorizations={3}; interactions={4}.");
    private static readonly string[] AccessClaims = ["iss", Subject, "iat", "exp", "nbf", "jti", ClientId, Scope, AuthorizationId, TokenId, Presenter];


    /// <summary>Retains the original native caller contract while borrowing its already-created registration and flow bindings.</summary>
    internal static Task ValidateAsync(ManagedCacheTestContext test, ManagedTokenFlow flow, JsonElement body, string subject, bool applicationOnly = false, bool multipleFlows = false, ManagedTokenProjection? projection = null) =>
        ValidateAsync(ManagedTokenEvidenceContext.Borrow(test), flow.Nonce, flow.RequestId, body, subject, applicationOnly, multipleFlows, projection);

    /// <summary>Checks signatures, lifetime, issuer, ID-token recipient and nonce/hash bindings without printing credential contents.</summary>
    internal static async Task ValidateAsync(ManagedTokenEvidenceContext test, string nonce, string requestId, JsonElement body, string subject, bool applicationOnly = false, bool multipleFlows = false, ManagedTokenProjection? projection = null)
    {
        var verified = await ManagedIdentityTokenOracle.ValidateAsync(test.Transport, test.Environment.Profile.Issuer,
            test.ClientIdentifier, nonce, subject, body, projection);
        var identity = verified.Principal;
        var keys = verified.SigningKeys;
        var access = body.GetProperty(AccessField).GetString()!;
        var accessParameters = ManagedTokenValidation.Parameters(test.Environment.Profile.Issuer, keys, AccessType);
        accessParameters.ValidateAudience = projection?.Resource is not null;
        accessParameters.ValidAudience = projection?.Resource; // Absent resource still has the original audience-free claim allowlist.
        var credentials = test.Environment.IssuerServices.GetRequiredService<IssuerCredentials>();
        accessParameters.TokenDecryptionKeys = credentials.EncryptionKeys.All.Select(certificate => new X509SecurityKey(certificate));
        var principal = await ManagedTokenValidation.ValidateAsync(access, accessParameters, projection?.Resource is null ? AccessClaims : [.. AccessClaims, Audience]);
        Assert.True(principal.GetClaim(Subject) == subject && principal.GetClaim(ClientId) == test.ClientIdentifier && principal.GetClaim(Scope) == (projection?.Scope ?? OpenId));
        Assert.True(principal.GetClaim(Presenter) == test.ClientIdentifier);
        Assert.True(principal.GetClaim(Audience) == projection?.Resource);
        var all = await test.SnapshotAsync(applicationOnly);
        var interaction = Assert.Single(all.Interactions, row => row.RequestId == requestId);
        Assert.NotNull(interaction.AuthorizationId);
        var snapshot = multipleFlows
            ? new ManagedCacheSnapshot([interaction], all.Tokens.Where(row => row.Authorization?.Id == interaction.AuthorizationId).ToArray(), all.Authorizations, all.Sessions)
            : all;
        var identities = snapshot.Tokens.Count(value => value.Type == StoredIdentityType);
        var accesses = snapshot.Tokens.Count(value => value.Type == StoredAccessType);
        Assert.True(identities == 1 && accesses == 1, string.Format(CultureInfo.InvariantCulture, Cardinality,
            identities, accesses, all.Tokens.Length, all.Authorizations, all.Interactions.Length));
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
