using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Keys;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityPlatform.Host.Tests.Features.Issuer;

public sealed partial class ActiveKeyPolicyTests
{
    [Theory]
    [InlineData(TokenTypeIdentifiers.Private.AuthorizationCode, 60, true)]
    [InlineData(TokenTypeIdentifiers.Private.RequestToken, 300, true)]
    [InlineData(TokenTypeIdentifiers.AccessToken, 300, false)]
    [InlineData(TokenTypeIdentifiers.IdentityToken, 300, false)]
    public async Task Explicit_selection_overrides_native_sort_without_mutating_native_validation_inventories(string type, int maximum, bool encrypted)
    {
        using var fixture = new PolicyFixture();
        var options = fixture.Options;
        var signingOrder = options.SigningCredentials.ToArray();
        var encryptionOrder = options.EncryptionCredentials.ToArray();
        Assert.Equal(fixture.Credentials.SigningKeys.Staged!.Thumbprint, signingOrder[0].Key.KeyId);
        Assert.Equal(fixture.Credentials.EncryptionKeys.Staged!.Thumbprint, encryptionOrder[0].Key.KeyId);
        var context = await fixture.ContextAsync(type, fixture.Now, fixture.Now.AddSeconds(maximum));
        Assert.Equal(signingOrder[0], context.SigningCredentials);
        await ActiveKeyPolicy.ApplyAsync(context, fixture.Credentials);
        Assert.False(context.IsRejected);
        Assert.Equal(fixture.Credentials.Signing.Thumbprint, context.SigningCredentials!.Key.KeyId);
        Assert.True(encrypted ? context.EncryptionCredentials?.Key.KeyId == fixture.Credentials.Encryption.Thumbprint : context.EncryptionCredentials is null);
        Assert.Equal(signingOrder, options.SigningCredentials);
        Assert.Equal(encryptionOrder, options.EncryptionCredentials);
        Assert.Equal(2, options.TokenValidationParameters.IssuerSigningKeys.Count());
        Assert.Equal(2, options.TokenValidationParameters.TokenDecryptionKeys.Count());
    }

    [Theory]
    [InlineData(TokenTypeIdentifiers.Private.AuthorizationCode, 60)]
    [InlineData(TokenTypeIdentifiers.Private.RequestToken, 300)]
    [InlineData(TokenTypeIdentifiers.AccessToken, 300)]
    [InlineData(TokenTypeIdentifiers.IdentityToken, 300)]
    public async Task Invalid_actual_principal_dates_are_rejected_without_rewriting_or_fallback(string type, int maximum)
    {
        using var fixture = new PolicyFixture();
        (DateTimeOffset? Created, DateTimeOffset? Expires)[] dates =
        [
            (null, fixture.Now.AddSeconds(maximum)), (fixture.Now, null),
            (fixture.Now, fixture.Now), (fixture.Now, fixture.Now.AddSeconds(-1)),
            (fixture.Now.AddSeconds(1), fixture.Now.AddSeconds(maximum)),
            (fixture.Now.AddSeconds(-maximum), fixture.Now),
            (fixture.Now, fixture.Now.AddSeconds(maximum + 1))
        ];
        foreach (var (created, expires) in dates)
        {
            var context = await fixture.ContextAsync(type, created, expires);
            await ActiveKeyPolicy.ApplyAsync(context, fixture.Credentials);
            RequireDenied(context);
            Assert.Equal(created, context.Principal.GetCreationDate());
            Assert.Equal(expires, context.Principal.GetExpirationDate());
        }
        var valid = await fixture.ContextAsync(type, fixture.Now, fixture.Now.AddSeconds(maximum));
        await ActiveKeyPolicy.ApplyAsync(valid, fixture.Credentials);
        Assert.False(valid.IsRejected);
    }

    [Theory]
    [InlineData("expired-signing")]
    [InlineData("future-signing")]
    [InlineData("insufficient-tail")]
    [InlineData("expired-encryption")]
    [InlineData("future-encryption")]
    [InlineData("missing-active-signing")]
    [InlineData("missing-active-encryption")]
    [InlineData("missing-encryption")]
    [InlineData("unexpected-encryption")]
    [InlineData("algorithm-change")]
    [InlineData("unsupported-type")]
    public async Task Unsuitable_generation_state_fails_closed_without_selecting_staged_keys(string scenario)
    {
        using var fixture = new PolicyFixture(shortEncryption: scenario is "expired-encryption" or "future-encryption");
        var type = TokenTypeIdentifiers.Private.AuthorizationCode;
        fixture.Clock.Now = scenario switch
        {
            "expired-signing" => new DateTimeOffset(fixture.Credentials.Signing.NotAfter.ToUniversalTime()),
            "future-signing" => new DateTimeOffset(fixture.Credentials.Signing.NotBefore.ToUniversalTime()).AddSeconds(-1),
            "insufficient-tail" => new DateTimeOffset(fixture.Credentials.Signing.NotAfter.ToUniversalTime()).AddSeconds(-30),
            "future-encryption" => new DateTimeOffset(fixture.Credentials.Encryption.NotBefore.ToUniversalTime()).AddSeconds(-1),
            "expired-encryption" => new DateTimeOffset(fixture.Credentials.Encryption.NotAfter.ToUniversalTime()),
            _ => fixture.Now
        };
        if (scenario == "unsupported-type")
        {
            type = TokenTypeIdentifiers.RefreshToken;
        }
        if (scenario == "unexpected-encryption")
        {
            type = TokenTypeIdentifiers.IdentityToken;
        }
        var context = await fixture.ContextAsync(type, fixture.Clock.Now, fixture.Clock.Now.AddSeconds(60));
        if (scenario == "missing-encryption")
        {
            context.EncryptionCredentials = null;
        }
        if (scenario == "unexpected-encryption")
        {
            context.EncryptionCredentials = fixture.Options.EncryptionCredentials[0];
        }
        if (scenario == "algorithm-change")
        {
            fixture.Options.SigningCredentials.Add(new SigningCredentials(fixture.Options.SigningCredentials[0].Key, SecurityAlgorithms.RsaSha512));
        }
        if (scenario == "missing-active-signing")
        {
            fixture.Options.SigningCredentials.RemoveAll(value => value.Key.KeyId == fixture.Credentials.Signing.Thumbprint);
        }
        if (scenario == "missing-active-encryption")
        {
            fixture.Options.EncryptionCredentials.RemoveAll(value => value.Key.KeyId == fixture.Credentials.Encryption.Thumbprint);
        }
        await ActiveKeyPolicy.ApplyAsync(context, fixture.Credentials);
        RequireDenied(context);
    }

    private static void RequireDenied(GenerateTokenContext context)
    {
        Assert.True(context.IsRejected);
        Assert.Equal(Errors.ServerError, context.Error);
        Assert.Equal(ActiveKeyPolicy.SafeDescription, context.ErrorDescription);
        Assert.Null(context.Token);
    }

    private sealed class PolicyFixture : IDisposable
    {
        private readonly IssuerMaterialFixture material = new();
        private readonly ServiceProvider services;
        public DateTimeOffset Now { get; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        public PolicyClock Clock
        {
            get;
        }
        public IssuerCredentials Credentials
        {
            get;
        }
        public OpenIddictServerOptions Options
        {
            get;
        }

        public PolicyFixture(bool shortEncryption = false)
        {
            material.AddCertificate("Signing:Staged", X509KeyUsageFlags.DigitalSignature, notAfter: Now.AddDays(60));
            material.AddCertificate("Encryption:Staged", X509KeyUsageFlags.KeyEncipherment, notAfter: Now.AddDays(60));
            if (shortEncryption)
            {
                material.AddCertificate("Encryption", X509KeyUsageFlags.KeyEncipherment, notBefore: Now.AddSeconds(-1), notAfter: Now.AddDays(1));
            }
            Clock = new PolicyClock { Now = Now };
            var registrations = new ServiceCollection().AddLogging();
            registrations.AddIdentityPersistence(DatabaseProvider.PostgreSql, "Host=localhost;Database=synthetic");
            Credentials = registrations.AddIssuerProtection(material.Configuration)!;
            registrations.AddFirstPartyPilot(Credentials);
            registrations.Configure<OpenIddictServerOptions>(options => options.TimeProvider = Clock);
            services = registrations.BuildServiceProvider();
            Options = services.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;
        }

        public async Task<GenerateTokenContext> ContextAsync(string type, DateTimeOffset? created, DateTimeOffset? expires)
        {
            var context = new GenerateTokenContext(new OpenIddictServerTransaction { Options = Options, Logger = NullLogger.Instance })
            {
                TokenType = type,
                Principal = new ClaimsPrincipal(new ClaimsIdentity()).SetCreationDate(created).SetExpirationDate(expires)
            };
            await new OpenIddictServerHandlers.Protection.AttachSecurityCredentials().HandleAsync(context);
            return context;
        }

        public void Dispose()
        {
            services.Dispose();
            Credentials.Dispose();
            material.Dispose();
        }
    }

    private sealed class PolicyClock : TimeProvider
    {
        public DateTimeOffset Now
        {
            get; set;
        }
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
