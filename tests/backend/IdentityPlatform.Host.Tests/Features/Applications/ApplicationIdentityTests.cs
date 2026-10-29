using System.Security.Claims;
using IdentityPlatform.Admin.Api.Features.Authentication;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Exercises the API's post-native binding policy independently of token cryptography, which genuine issuer tests verify.</summary>
public sealed class ApplicationIdentityTests
{
    private const string Scheme = "validated-native-test";
    private const string Subject = "synthetic-account";
    private const string Session = "synthetic-session";
    private const string Token = "stored-token";
    private const string Authorization = "stored-authorization";
    private const string Client = "identity-admin-bff";
    private const string Scope = "identity.admin";
    private const string Audience = "identity-admin-api";
    private const string SignedExpiration = "exp";
    private const string SessionClaim = "sid";
    private const string Other = "other-binding";
    private const int LifetimeSeconds = 300;
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The minimal legitimate native result binds the exact public client, presenters, resource, subject and stored lifecycle identifiers.</summary>
    [Fact]
    public void Given_CompleteNativeIdentity_When_ApiPolicyReads_Then_ExactOriginalBindingsAreRetained()
    {
        var value = ApiIdentity.Read(Principal(), Now);
        Assert.NotNull(value);
        Assert.Equal(Subject, value.Subject);
        Assert.Equal(Session, value.Session);
        Assert.Equal(Token, value.TokenId);
        Assert.Equal(Authorization, value.AuthorizationId);
        Assert.Equal(Now.AddSeconds(LifetimeSeconds), value.ExpiresAt);
    }

    /// <summary>A persisted future expiry cannot restore an expired signed lifetime, and a shorter signed lifetime remains the publication bound.</summary>
    [Fact]
    public void Given_RestoredPersistedExpiry_When_SignedExpiryDiffers_Then_EarlierOriginalDeadlineControls()
    {
        Assert.Null(ApiIdentity.Read(Principal().SetClaim(SignedExpiration, Now.ToUnixTimeSeconds()), Now));
        Assert.Null(ApiIdentity.Read(Principal().SetClaim(SignedExpiration, (string?)null), Now));
        Assert.Null(ApiIdentity.Read(Principal().SetClaim(SignedExpiration, long.MaxValue), Now));
        var earlier = Now.AddSeconds(1);
        Assert.Equal(earlier, ApiIdentity.Read(Principal().SetClaim(SignedExpiration, earlier.ToUnixTimeSeconds()), Now)!.ExpiresAt);
    }

    /// <summary>Private native presenter claims cannot conceal contradictory or missing standard client identity.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(Other)]
    public void Given_ConflictingPublicClient_When_PrivatePresenterMatches_Then_IdentityIsRejected(string? client)
    {
        var principal = Principal().SetClaim(OpenIddictConstants.Claims.ClientId, client);
        Assert.Null(ApiIdentity.Read(principal, Now));
    }

    /// <summary>Each missing or inconsistent native authorization dimension independently rejects the result.</summary>
    [Fact]
    public void Given_NativeBindingOmitted_When_ApiPolicyReads_Then_NoIdentityIsProduced()
    {
        Assert.Null(ApiIdentity.Read(Principal().SetAudiences(Other), Now));
        Assert.Null(ApiIdentity.Read(Principal().SetScopes(Other), Now));
        Assert.Null(ApiIdentity.Read(Principal().SetPresenters(Other), Now));
        Assert.Null(ApiIdentity.Read(Principal().SetTokenId(null), Now));
        Assert.Null(ApiIdentity.Read(Principal().SetAuthorizationId(null), Now));
        Assert.Null(ApiIdentity.Read(Principal().SetClaim(OpenIddictConstants.Claims.Subject, (string?)null), Now));
        Assert.Null(ApiIdentity.Read(Principal().SetClaim(SessionClaim, (string?)null), Now));
        Assert.Null(ApiIdentity.Read(Principal().SetExpirationDate(null), Now));
        Assert.Null(ApiIdentity.Read(Principal().SetExpirationDate(Now), Now));
        Assert.Null(ApiIdentity.Read(new ClaimsPrincipal(new ClaimsIdentity(Principal().Claims)), Now));
    }

    /// <summary>Duplicate and differently cased subject/session claims cannot silently select one account or source.</summary>
    [Theory]
    [InlineData("sub")]
    [InlineData("SUB")]
    [InlineData(SessionClaim)]
    [InlineData("SID")]
    public void Given_AmbiguousNativeSubject_When_ApiPolicyReads_Then_NoIdentityIsProduced(string alias)
    {
        var principal = Principal();
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(alias, Other));
        Assert.Null(ApiIdentity.Read(principal, Now));
    }

    /// <summary>Creates policy inputs with native extension methods; this helper supplies no cryptographic acceptance evidence.</summary>
    private static ClaimsPrincipal Principal() => new ClaimsPrincipal(new ClaimsIdentity(Scheme))
        .SetClaim(OpenIddictConstants.Claims.Subject, Subject).SetClaim(SessionClaim, Session)
        .SetClaim(OpenIddictConstants.Claims.ClientId, Client).SetPresenters(Client).SetScopes(Scope).SetAudiences(Audience)
        .SetTokenId(Token).SetAuthorizationId(Authorization).SetClaim(SignedExpiration, Now.AddSeconds(LifetimeSeconds).ToUnixTimeSeconds())
        .SetExpirationDate(Now.AddSeconds(LifetimeSeconds));
}
