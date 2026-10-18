using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.Client.AspNetCore;
using OpenIddict.Client.SystemNetHttp;
using OpenIddict.Core;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Exercises actual native option finalization and cryptographic issuer validation for dormant client composition.</summary>
public sealed class NativeClientCompositionTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string Connection = "Host=localhost;Database=synthetic_client_composition";
    private const string Subject = "synthetic-administrator";
    private const int LifetimeSeconds = 60;
    private const int StateLifetimeSeconds = 300;
    private const string OpenId = "openid";
    private const string CookieName = "__Host-identity-admin-client-state";
    private const string CookiePath = "/";

    /// <summary>Final native options preserve explicit storage, separate key roles, exact profile and correlation/transport restrictions.</summary>
    [Fact]
    public async Task Given_DormantClientComposition_When_NativeOptionsFinalize_Then_ProfileAndOwnershipRemainExplicit()
    {
        using var credentials = NativeClientProfileTests.Load(fixture);
        await using var provider = Create(credentials);
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var registration = Assert.Single(options.Registrations);
        Assert.False(options.DisableTokenStorage);
        Assert.False(registration.DisablePushedAuthorizationRequests);
        Assert.Null(registration.ClientSecret);
        Assert.Equal("identity-admin-bff", registration.ClientId);
        Assert.Equal(["openid", "identity.admin"], registration.Scopes.OrderBy(value => value == OpenId ? 0 : 1));
        Assert.Equal(["private_key_jwt"], registration.ClientAuthenticationMethods);
        Assert.Equal(["code"], registration.ResponseTypes);
        Assert.Equal(["query"], registration.ResponseModes);
        Assert.Equal(["S256"], registration.CodeChallengeMethods);
        Assert.Equal(TimeSpan.FromSeconds(StateLifetimeSeconds), options.StateTokenLifetime);
        Assert.Equal(TimeSpan.FromSeconds(LifetimeSeconds), options.ClientAssertionLifetime);
        Assert.Same(credentials.Assertion, Assert.IsType<X509SecurityKey>(Assert.Single(registration.SigningCredentials).Key).Certificate);
        Assert.Same(credentials.StateSigning.Active, Assert.IsType<X509SecurityKey>(Assert.Single(options.SigningCredentials).Key).Certificate);
        Assert.Same(credentials.StateEncryption.Active, Assert.IsType<X509SecurityKey>(Assert.Single(options.EncryptionCredentials).Key).Certificate);
        Assert.True(provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue.DisableEntityCaching);
        Assert.False(provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue.DisableAdditionalFiltering);
        RequireTransport(provider);
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<OwnedClientDispatcher>(scope.ServiceProvider.GetRequiredService<IOpenIddictClientDispatcher>());
    }

    /// <summary>Actual signed tokens with the alternate root spelling are rejected after native validation-parameter resolution.</summary>
    [Theory]
    [InlineData(false, false, "JWT", true)]
    [InlineData(false, true, "JWT", false)]
    [InlineData(false, false, "at+jwt", false)]
    [InlineData(false, false, "oi_stet+jwt", false)]
    [InlineData(true, false, "oi_stet+jwt", true)]
    [InlineData(true, true, "oi_stet+jwt", false)]
    [InlineData(true, false, "JWT", false)]
    public async Task Given_ExactNativeIssuerPolicy_When_ValidatingSignedTokens_Then_AlternateRootSpellingIsDenied(bool state, bool alternate, string type, bool valid)
    {
        using var credentials = NativeClientProfileTests.Load(fixture);
        await using var provider = Create(credentials);
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var registration = Assert.Single(options.Registrations);
        var issuer = state ? NativeClientProfileTests.Client : NativeClientProfileTests.Issuer;
        var signing = state ? Assert.Single(options.SigningCredentials) : Assert.Single(registration.SigningCredentials);
        var now = DateTimeOffset.UtcNow;
        var token = options.JsonWebTokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new(OpenIddictConstants.Claims.Subject, Subject)]),
            Issuer = alternate ? issuer.TrimEnd('/') : issuer,
            Audience = registration.ClientId,
            IssuedAt = now.UtcDateTime,
            Expires = now.AddSeconds(LifetimeSeconds).UtcDateTime,
            SigningCredentials = signing,
            TokenType = type
        });
        var configuration = new OpenIddictConfiguration { Issuer = registration.Issuer };
        configuration.SigningKeys.Add(signing.Key);
        var transaction = new OpenIddictClientTransaction { Options = options, Registration = registration, Configuration = configuration, Logger = NullLogger.Instance };
        var context = new OpenIddictClientEvents.ValidateTokenContext(transaction) { Token = token };
        context.ValidTokenTypes.Add(state ? OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken : OpenIddictConstants.TokenTypeIdentifiers.IdentityToken);
        await new OpenIddictClientHandlers.Protection.ResolveTokenValidationParameters().HandleAsync(context);
        await new NativeClientValidationParameters(provider.GetRequiredService<NativeClientProfile>()).HandleAsync(context);
        await new OpenIddictClientHandlers.Protection.ValidateIdentityModelToken().HandleAsync(context);
        Assert.Equal(!valid, context.IsRejected);
        Assert.Equal(valid, context.Principal is not null);
    }

    /// <summary>Checks real native transport settings and the browser correlation builder without creating product routes or cookies.</summary>
    private static void RequireTransport(ServiceProvider provider)
    {
        var http = provider.GetRequiredService<IOptionsMonitor<OpenIddictClientSystemNetHttpOptions>>().CurrentValue;
        Assert.Null(http.HttpErrorPolicy);
        Assert.Null(http.HttpResiliencePipeline);
        var cookie = provider.GetRequiredService<IOptionsMonitor<OpenIddictClientAspNetCoreOptions>>().CurrentValue.CookieBuilder;
        Assert.Equal(CookieName, cookie.Name);
        Assert.Equal(CookiePath, cookie.Path);
        Assert.Null(cookie.Domain);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, cookie.SameSite);
    }

    /// <summary>Builds actual services against an unused synthetic connection; these controls never execute database operations.</summary>
    internal static ServiceProvider Create(NativeClientCredentials credentials) => new ServiceCollection().AddLogging()
        .AddSessionPersistence(DatabaseProvider.PostgreSql, Connection)
        .AddAdministratorClient(NativeClientProfile.Create(NativeClientProfileTests.Issuer, NativeClientProfileTests.Client,
            NativeClientProfileTests.Callback, NativeClientProfileTests.KeyId, credentials), credentials).BuildServiceProvider(validateScopes: true);
}
