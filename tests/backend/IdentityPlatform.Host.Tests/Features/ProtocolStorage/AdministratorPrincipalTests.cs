using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks minimal destinations and retention of native protocol identity without treating projection as live authority.</summary>
public sealed class AdministratorPrincipalTests
{
    private const string SessionId = "sid";
    private const string User = "principal-user";
    private const string Session = "principal-session";
    private const string Authorization = "native-authorization";
    private const string Token = "native-token";
    private const string Nonce = "native-nonce";
    private const string Extra = "permission";
    private const string ExtraValue = "untrusted-permission";
    private const long Revision = 7;
    private const string Issuer = "https://principal.example.test/";
    private const string Client = "identity-admin-bff";
    private const string PrivatePrefix = "oi_admin_";
    private const int TokenSeconds = 300;
    private const string ArrayMethods = "[\"pwd\",\"otp\"]";
    private const string JsonArray = "JSON_ARRAY";
    private const string MaximumAgeAlias = "OI_ADMIN_MAXIMUM_AGE";
    private static readonly ProtocolAdmissionClaims Current = new(User, Session, 100, 101, 1000, 160, 400);

    /// <summary>Only sub/sid reach both public token types; authentication evidence is identity-token only and private material stays private.</summary>
    [Fact]
    public void Given_NativeBindingsAndUnrelatedClaims_When_ProjectingCurrentAdmission_Then_MinimalDestinationsPreserveNativeIdentity()
    {
        var native = new ClaimsPrincipal(new ClaimsIdentity()).SetAuthorizationId(Authorization).SetTokenId(Token)
            .SetClaim(OpenIddictConstants.Claims.Private.Nonce, Nonce).SetClaim(Extra, ExtraValue)
            .SetClaim(OpenIddictConstants.Claims.Email, ExtraValue).SetClaim(OpenIddictConstants.Claims.Role, ExtraValue);
        var projected = AdministratorPrincipal.Project(native, Current, Revision, null);
        Assert.Equal(Authorization, projected.GetAuthorizationId());
        Assert.Equal(Token, projected.GetTokenId());
        Assert.Equal(Nonce, projected.GetClaim(OpenIddictConstants.Claims.Private.Nonce));
        Assert.DoesNotContain(projected.Claims, claim => claim.Type is Extra or OpenIddictConstants.Claims.Email or OpenIddictConstants.Claims.Role);
        Assert.Equal([OpenIddictConstants.Claims.Subject, SessionId], projected.Claims
            .Where(claim => claim.HasDestination(OpenIddictConstants.Destinations.AccessToken)).Select(claim => claim.Type));
        Assert.Equal(5, projected.Claims.Count(claim => claim.HasDestination(OpenIddictConstants.Destinations.IdentityToken)));
        Assert.DoesNotContain(projected.Claims.Where(claim => claim.Type.StartsWith(OpenIddictConstants.Claims.Prefixes.Private, StringComparison.Ordinal)),
            claim => claim.GetDestinations().Length != 0);
        Assert.True(AdministratorPrincipal.Matches(projected, Current, Revision));
        Assert.False(AdministratorPrincipal.Matches(projected, Current with
        {
            FactorVerifiedAt = Current.FactorVerifiedAt + 1
        }, Revision));
        Assert.False(AdministratorPrincipal.Matches(projected, Current, Revision + 1));
        Assert.True(native.HasClaim(Extra, ExtraValue));
    }

    /// <summary>Duplicate original proof fields fail even when their strings are equal and a convenience accessor could normalize them.</summary>
    [Theory]
    [InlineData("sub")]
    [InlineData("sid")]
    [InlineData("auth_time")]
    [InlineData("oi_admin_factor_time")]
    [InlineData("oi_admin_source_expiry")]
    [InlineData("oi_admin_profile_revision")]
    [InlineData("amr")]
    public void Given_DuplicatedProtectedField_When_MatchingCurrentAuthority_Then_ExchangeCannotRenewProof(string field)
    {
        var principal = AdministratorPrincipal.Project(new ClaimsPrincipal(new ClaimsIdentity()), Current, Revision, null);
        ((ClaimsIdentity)principal.Identity!).AddClaim(principal.Claims.First(claim => claim.Type == field).Clone());
        Assert.False(AdministratorPrincipal.Matches(principal, Current, Revision));
    }

    /// <summary>Protected requested age remains optional and scalar; malformed or duplicated values cannot widen freshness.</summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("0", true)]
    [InlineData("60", true)]
    [InlineData("301", true)]
    [InlineData("-1", false)]
    [InlineData("01", false)]
    [InlineData("1.0", false)]
    [InlineData("9223372036854775808", false)]
    public void Given_ProtectedAge_When_ReadForExchange_Then_OnlyOneCanonicalNonnegativeIntegerIsAccepted(string? value, bool accepted)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetClaim(AdministratorPrincipal.MaximumAge, value);
        Assert.Equal(accepted, AdministratorPrincipal.TryReadMaximumAge(principal, out _));
        if (value is not null)
        {
            ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(AdministratorPrincipal.MaximumAge, value));
            Assert.False(AdministratorPrincipal.TryReadMaximumAge(principal, out _));
        }
    }

    /// <summary>The installed native preparation handlers remove every private administrator field from public token principals.</summary>
    [Fact]
    public async Task Given_ProjectedCodeClaims_When_NativePublicPrincipalsArePrepared_Then_PrivateProofNeverLeavesCode()
    {
        var native = new ClaimsPrincipal(new ClaimsIdentity()).SetAuthorizationId(Authorization)
            .SetClaim(OpenIddictConstants.Claims.Private.Nonce, Nonce);
        var projected = AdministratorPrincipal.Project(native, Current, Revision, TokenSeconds)
            .SetAccessTokenLifetime(TimeSpan.FromSeconds(TokenSeconds)).SetIdentityTokenLifetime(TimeSpan.FromSeconds(TokenSeconds));
        var context = new OpenIddictServerEvents.ProcessSignInContext(new OpenIddictServerTransaction
        {
            Options = new OpenIddictServerOptions { Issuer = new Uri(Issuer), TimeProvider = TimeProvider.System },
            Logger = NullLogger.Instance,
            EndpointType = OpenIddictServerEndpointType.Token,
            Request = new OpenIddictRequest { ClientId = Client, GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode }
        })
        {
            Principal = projected
        };
        await new OpenIddictServerHandlers.PrepareAccessTokenPrincipal().HandleAsync(context);
        await new OpenIddictServerHandlers.PrepareIdentityTokenPrincipal().HandleAsync(context);
        foreach (var principal in new[] { context.AccessTokenPrincipal, context.IdentityTokenPrincipal })
        {
            Assert.NotNull(principal);
            Assert.DoesNotContain(principal.Claims, claim => claim.Type.StartsWith(PrivatePrefix, StringComparison.Ordinal));
            Assert.Equal(User, principal.GetClaim(OpenIddictConstants.Claims.Subject));
            Assert.Equal(Session, principal.GetClaim(SessionId));
            Assert.Equal(Authorization, principal.GetAuthorizationId());
        }
        Assert.Equal(Nonce, context.IdentityTokenPrincipal!.GetClaim(OpenIddictConstants.Claims.Nonce));
        Assert.Null(context.AccessTokenPrincipal!.GetClaim(OpenIddictConstants.Claims.AuthenticationTime));
        Assert.Empty(context.AccessTokenPrincipal!.GetClaims(OpenIddictConstants.Claims.AuthenticationMethodReference));
        Assert.Equal(2, context.IdentityTokenPrincipal!.GetClaims(OpenIddictConstants.Claims.AuthenticationMethodReference).Length);
    }

    /// <summary>Native array expansion cannot turn repeated raw array claims into canonical product authentication evidence.</summary>
    [Fact]
    public void Given_DuplicatedArrayMethods_When_MatchingCurrentAuthority_Then_ArrayExpansionCannotHideMultiplicity()
    {
        var principal = AdministratorPrincipal.Project(new ClaimsPrincipal(new ClaimsIdentity()), Current, Revision, null);
        principal.SetClaims(OpenIddictConstants.Claims.AuthenticationMethodReference, []);
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.AuthenticationMethodReference, ArrayMethods, JsonArray));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.AuthenticationMethodReference, ArrayMethods, JsonArray));
        Assert.False(AdministratorPrincipal.Matches(principal, Current, Revision));
    }

    /// <summary>A case-aliased protected age is malformed rather than an omitted request that could widen freshness.</summary>
    [Fact]
    public void Given_CaseAliasedProtectedAge_When_ReadForExchange_Then_ItCannotBecomeAnOmittedAge()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetClaim(MaximumAgeAlias, 0L);
        Assert.False(AdministratorPrincipal.TryReadMaximumAge(principal, out _));
    }
}
