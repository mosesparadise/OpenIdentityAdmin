using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Verifies callback policy against actual native state preparation, protected serialization and cryptographic validation.</summary>
public sealed class NativeClientStateRoundTripTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string Subject = "synthetic-state-subject";
    private const string AdministratorScope = "identity.admin";
    private const string WrongIssuer = "https://untrusted.example.test/";

    /// <summary>Native serialization replaces its private issuer input with the public iss claim; only that validated exact issuer admits a receipt.</summary>
    [Fact]
    public async Task Given_ActualNativeProtectedState_When_CallbackReceiptIsAttached_Then_ValidatedPublicIssuerIsRequired()
    {
        using var keys = NativeClientProfileTests.Load(fixture);
        await using var services = NativeClientCompositionTests.Create(keys);
        var options = services.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var challenge = new OpenIddictClientEvents.ProcessChallengeContext(Transaction(options))
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity([new(OpenIddictConstants.Claims.Subject, Subject)])),
            GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode,
            ResponseType = OpenIddictConstants.ResponseTypes.Code,
            RedirectUri = NativeClientProfileTests.Callback
        };
        challenge.Scopes.UnionWith([OpenIddictConstants.Scopes.OpenId, AdministratorScope]);
        await new OpenIddictClientHandlers.PrepareLoginStateTokenPrincipal().HandleAsync(challenge);
        Assert.True(challenge.StateTokenPrincipal!.GetClaim(OpenIddictConstants.Claims.Private.Issuer) == NativeClientProfileTests.Client);
        var token = await NativeClientRetainedKeyTests.GenerateAsync(services, keys, challenge.StateTokenPrincipal);
        var principal = await NativeClientRetainedKeyTests.ValidateAsync(options, token);
        Assert.Null(principal.GetClaim(OpenIddictConstants.Claims.Private.Issuer));
        Assert.True(principal.GetClaim(OpenIddictConstants.Claims.Issuer) == NativeClientProfileTests.Client);
        var profile = services.GetRequiredService<NativeClientProfile>();
        var context = new OpenIddictClientEvents.ProcessAuthenticationContext(Transaction(options)) { StateTokenPrincipal = principal };
        NativeClientCallback.Attach(context, profile);
        NativeClientCallback.Find(context).Require(context);
        foreach (var issuer in new[] { WrongIssuer, null })
        {
            var changed = principal.Clone();
            changed.SetClaim(OpenIddictConstants.Claims.Issuer, issuer);
            changed.SetClaim(OpenIddictConstants.Claims.Private.Issuer, NativeClientProfileTests.Client);
            var rejected = new OpenIddictClientEvents.ProcessAuthenticationContext(Transaction(options)) { StateTokenPrincipal = changed };
            Assert.Throws<InvalidOperationException>(() => NativeClientCallback.Attach(rejected, profile));
        }
    }

    /// <summary>Uses finalized native registration/options and a real request container while suppressing credential-bearing native diagnostics.</summary>
    private static OpenIddictClientTransaction Transaction(OpenIddictClientOptions options) => new()
    {
        Options = options,
        Registration = Assert.Single(options.Registrations),
        Logger = NullLogger.Instance,
        Request = new()
    };
}
