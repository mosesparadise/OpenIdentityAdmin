using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Proves real native crypto output survives state-key replacement while new issuance uses explicitly active material.</summary>
public sealed class NativeClientRetainedKeyTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string Section = "ClientCredentials";
    private const string Subject = "synthetic-state-subject";
    private const int LifetimeSeconds = 300;

    /// <summary>Old encrypted state validates after restart/replacement and current issuance ignores the native preference for longer-lived old keys.</summary>
    [Fact]
    public async Task Given_RetainedStateCredentials_When_KeysAreReplaced_Then_OldStateValidatesAndNewStateUsesOnlyActiveKeys()
    {
        using var previous = NativeClientProfileTests.Load(fixture);
        string token;
        await using (var original = NativeClientCompositionTests.Create(previous))
        {
            token = await GenerateAsync(original, previous);
            await ValidateAsync(original.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue, token);
        }
        var settings = new ConfigurationBuilder().AddInMemoryCollection(fixture.RotatedSettings()).Build();
        using var replacement = NativeClientCredentials.Load(settings.GetSection(Section), DateTimeOffset.UtcNow);
        await using var restarted = NativeClientCompositionTests.Create(replacement);
        var options = restarted.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        Assert.Equal(2, options.SigningCredentials.Count);
        Assert.Equal(2, options.EncryptionCredentials.Count);
        Assert.NotSame(replacement.StateSigning.Active, Assert.IsType<X509SecurityKey>(options.SigningCredentials[0].Key).Certificate);
        await ValidateAsync(options, token);
        await ValidateAsync(options, await GenerateAsync(restarted, replacement));
    }

    /// <summary>Executes real native credential/subject/metadata/crypto handlers with no database persistence claimed by this pure control.</summary>
    internal static async Task<string> GenerateAsync(IServiceProvider provider, NativeClientCredentials credentials, ClaimsPrincipal? principal = null)
    {
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var now = DateTimeOffset.UtcNow;
        var transaction = new OpenIddictClientTransaction { Options = options, Registration = Assert.Single(options.Registrations), Logger = NullLogger.Instance };
        var context = new OpenIddictClientEvents.GenerateTokenContext(transaction)
        {
            Principal = principal ?? new ClaimsPrincipal(new ClaimsIdentity([new(OpenIddictConstants.Claims.Subject, Subject)]))
                .SetCreationDate(now).SetExpirationDate(now.AddSeconds(LifetimeSeconds))
                .SetClaim(OpenIddictConstants.Claims.Private.Issuer, NativeClientProfileTests.Client),
            TokenType = OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken,
            TokenFormat = OpenIddictConstants.TokenFormats.Private.JsonWebToken
        };
        await new OpenIddictClientHandlers.Protection.AttachSecurityCredentials().HandleAsync(context);
        await new NativeClientKeys(credentials).HandleAsync(context);
        Assert.Same(credentials.StateSigning.Active, Assert.IsType<X509SecurityKey>(context.SigningCredentials!.Key).Certificate);
        Assert.Same(credentials.StateEncryption.Active, Assert.IsType<X509SecurityKey>(context.EncryptionCredentials!.Key).Certificate);
        await new OpenIddictClientHandlers.Protection.AttachTokenSubject().HandleAsync(context);
        await new OpenIddictClientHandlers.Protection.AttachTokenMetadata().HandleAsync(context);
        await new OpenIddictClientHandlers.Protection.GenerateIdentityModelToken().HandleAsync(context);
        return Assert.IsType<string>(context.Token);
    }

    /// <summary>Resolves actual native validation/decryption keys from finalized restarted options and verifies both signature and ciphertext.</summary>
    internal static async Task<ClaimsPrincipal> ValidateAsync(OpenIddictClientOptions options, string token)
    {
        var transaction = new OpenIddictClientTransaction { Options = options, Registration = Assert.Single(options.Registrations), Logger = NullLogger.Instance };
        var context = new OpenIddictClientEvents.ValidateTokenContext(transaction) { Token = token };
        context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken);
        await new OpenIddictClientHandlers.Protection.ResolveTokenValidationParameters().HandleAsync(context);
        var validation = await context.SecurityTokenHandler.ValidateTokenAsync(token, context.TokenValidationParameters);
        Assert.True(validation.IsValid, validation.Exception?.GetType().Name);
        await new OpenIddictClientHandlers.Protection.ValidateIdentityModelToken().HandleAsync(context);
        Assert.False(context.IsRejected, context.ErrorDescription);
        Assert.Equal(Subject, context.Principal?.GetClaim(OpenIddictConstants.Claims.Subject));
        await new OpenIddictClientHandlers.Protection.MapInternalClaims().HandleAsync(context);
        return context.Principal!;
    }
}
