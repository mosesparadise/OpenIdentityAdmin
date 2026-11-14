using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Exercises each prepared native clone's independent identity-only policy; these synthetic controls do not replace real issuance tests.</summary>
public sealed class ManagedPreparedTests
{
    private const string Client = "managed-client";
    private const string Subject = "synthetic-subject";
    private const string Selector = "synthetic-selector";
    private const string Issuer = "https://issuer.example.test/";
    private const string Callback = "https://rp.example.test/callback";
    private const string Nonce = "original-nonce";
    private const string Authorization = "synthetic-authorization";
    private const string Foreign = "identity.admin";
    private const string Access = "synthetic-access-token";
    private const int Lifetime = 300;
    private const int HalfHash = 16;
    private static readonly string[] Types = [TokenTypeIdentifiers.Private.AuthorizationCode, TokenTypeIdentifiers.AccessToken, TokenTypeIdentifiers.IdentityToken];
    private static readonly ManagedPrincipalDefect[] Defects = [ManagedPrincipalDefect.Valid, ManagedPrincipalDefect.Role, ManagedPrincipalDefect.Scope,
        ManagedPrincipalDefect.Audience, ManagedPrincipalDefect.Actor];

    /// <summary>Every native token type has a positive control and rejects prepared role/scope/audience/delegation contamination.</summary>
    [Theory, MemberData(nameof(Cases))]
    public void Given_PreparedManagedClone_When_AuthorityIsInjected_Then_EveryTokenTypeEnforcesItsExactProjection(string type, ManagedPrincipalDefect defect)
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var row = new ManagedLoginInteraction { ApplicationId = Client, UserId = Subject, ApplicationRevision = 1, ExpiresAt = now.AddSeconds(Lifetime).ToUnixTimeSeconds() };
        var original = ManagedPrincipal.Create(row, Client, Selector).SetClaim(Claims.Private.Nonce, Nonce).SetAuthorizationId(Authorization);
        var transaction = new OpenIddictServerTransaction
        {
            Options = new() { Issuer = new Uri(Issuer), TimeProvider = TimeProvider.System },
            EndpointType = type == TokenTypeIdentifiers.Private.AuthorizationCode ? OpenIddictServerEndpointType.Authorization : OpenIddictServerEndpointType.Token,
            Request = new() { ClientId = Client, RedirectUri = Callback, Nonce = Nonce, CodeChallenge = Nonce, CodeChallengeMethod = CodeChallengeMethods.Sha256 }
        };
        transaction.SetProperty(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!,
            new OpenIddictServerEvents.ProcessAuthenticationContext(transaction) { AuthorizationCodePrincipal = original, RequestTokenPrincipal = original });
        var context = new OpenIddictServerEvents.ProcessSignInContext(transaction) { Principal = original, AccessToken = Access };
        var prepared = Prepare(row, type, now);
        switch (defect)
        {
            case ManagedPrincipalDefect.Role:
                prepared.SetClaim(Claims.Role, Foreign);
                break;
            case ManagedPrincipalDefect.Scope:
                prepared.SetScopes(Scopes.OpenId, Foreign);
                break;
            case ManagedPrincipalDefect.Audience:
                prepared.SetAudiences(Foreign);
                break;
            case ManagedPrincipalDefect.Actor:
                ((ClaimsIdentity)prepared.Identity!).Actor = new ClaimsIdentity(Foreign);
                break;
        }
        Assert.Equal(defect == ManagedPrincipalDefect.Valid, ManagedPreparedPrincipal.Matches(context, prepared, type, row, Selector, row.ExpiresAt));
    }

    /// <summary>Constructs only the separately documented native prepared shape for the selected token kind.</summary>
    private static ClaimsPrincipal Prepare(ManagedLoginInteraction row, string type, DateTimeOffset now)
    {
        var principal = type == TokenTypeIdentifiers.Private.AuthorizationCode ? ManagedPrincipal.Create(row, Client, Selector) :
            new ClaimsPrincipal(new ClaimsIdentity(Client)).SetClaim(Claims.Subject, Subject);
        principal.SetAuthorizationId(Authorization).SetCreationDate(now).SetExpirationDate(now.AddSeconds(Lifetime)).SetClaim(Claims.Private.Issuer, Issuer);
        if (type == TokenTypeIdentifiers.Private.AuthorizationCode)
        {
            principal.SetClaim(Claims.Private.RedirectUri, Callback).SetClaim(Claims.Private.CodeChallenge, Nonce)
                .SetClaim(Claims.Private.CodeChallengeMethod, CodeChallengeMethods.Sha256).SetClaim(Claims.Private.Nonce, Nonce);
        }
        else if (type == TokenTypeIdentifiers.AccessToken)
        {
            principal.SetScopes(Scopes.OpenId).SetPresenters(Client).SetClaim(Claims.ClientId, Client);
        }
        else
        {
            principal.SetAudiences(Client).SetClaim(Claims.AuthorizedParty, Client).SetClaim(Claims.Nonce, Nonce)
                .SetClaim(Claims.AccessTokenHash, Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(Access)).AsSpan(0, HalfHash).ToArray()));
        }
        return principal;
    }

    /// <summary>Preserves a separate metadata identity for every token kind and forbidden projection branch.</summary>
    public static IEnumerable<TheoryDataRow<string, ManagedPrincipalDefect>> Cases => from type in Types
                                                                                      from defect in Defects
                                                                                      select new TheoryDataRow<string, ManagedPrincipalDefect>(type, defect);
}
