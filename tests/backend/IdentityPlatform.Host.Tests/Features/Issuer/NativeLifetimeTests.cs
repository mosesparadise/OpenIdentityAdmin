using System.Collections.Immutable;
using System.Reflection;
using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.Keys;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityPlatform.Host.Tests.Features.Issuer;

public sealed partial class ActiveKeyPolicyTests
{
    [Theory]
    [InlineData(TokenTypeIdentifiers.Private.AuthorizationCode, Settings.TokenLifetimes.AuthorizationCode, 60)]
    [InlineData(TokenTypeIdentifiers.Private.RequestToken, Settings.TokenLifetimes.RequestToken, 300)]
    [InlineData(TokenTypeIdentifiers.AccessToken, Settings.TokenLifetimes.AccessToken, 300)]
    [InlineData(TokenTypeIdentifiers.IdentityToken, Settings.TokenLifetimes.IdentityToken, 300)]
    public async Task Native_principal_preparation_honors_client_setting_then_policy_enforces_maximum(string type, string setting, int maximum)
    {
        using var fixture = new PolicyFixture();
        foreach (var seconds in new[] { maximum, maximum + 1 })
        {
            var manager = DispatchProxy.Create<IOpenIddictApplicationManager, ClientSettingsProxy>();
            var stub = (ClientSettingsProxy)manager;
            stub.Settings = ImmutableDictionary<string, string>.Empty.Add(setting, TimeSpan.FromSeconds(seconds).ToString("c", System.Globalization.CultureInfo.InvariantCulture));
            var signIn = new ProcessSignInContext(new OpenIddictServerTransaction
            {
                Options = fixture.Options,
                Logger = NullLogger.Instance,
                EndpointType = OpenIddictServerEndpointType.Authorization,
                Request = new OpenIddictRequest { ClientId = "synthetic-client", Nonce = "synthetic-nonce" }
            })
            {
                Principal = new ClaimsPrincipal(new ClaimsIdentity()).SetClaim(Claims.Subject, "synthetic-subject")
            };
            IOpenIddictServerHandler<ProcessSignInContext> preparation = type switch
            {
                TokenTypeIdentifiers.Private.AuthorizationCode => new OpenIddictServerHandlers.PrepareAuthorizationCodePrincipal(manager),
                TokenTypeIdentifiers.Private.RequestToken => new OpenIddictServerHandlers.PrepareRequestTokenPrincipal(manager),
                TokenTypeIdentifiers.AccessToken => new OpenIddictServerHandlers.PrepareAccessTokenPrincipal(manager),
                _ => new OpenIddictServerHandlers.PrepareIdentityTokenPrincipal(manager)
            };
            await preparation.HandleAsync(signIn);
            var principal = type switch
            {
                TokenTypeIdentifiers.Private.AuthorizationCode => signIn.AuthorizationCodePrincipal,
                TokenTypeIdentifiers.Private.RequestToken => signIn.RequestTokenPrincipal,
                TokenTypeIdentifiers.AccessToken => signIn.AccessTokenPrincipal,
                _ => signIn.IdentityTokenPrincipal
            };
            Assert.NotNull(principal);
            Assert.Equal(TimeSpan.FromSeconds(seconds), principal.GetExpirationDate() - principal.GetCreationDate());
            Assert.Equal(2, stub.Calls);
            var generated = await fixture.ContextAsync(type, null, null);
            generated.Principal = principal;
            await ActiveKeyPolicy.ApplyAsync(generated, fixture.Credentials);
            if (seconds > maximum)
            {
                RequireDenied(generated);
            }
            else
            {
                Assert.False(generated.IsRejected);
                Assert.Equal(fixture.Credentials.Signing.Thumbprint, generated.SigningCredentials!.Key.KeyId);
            }
        }
    }

    // Only supplies application metadata; native preparation handlers remain real.
    public class ClientSettingsProxy : DispatchProxy
    {
        public ImmutableDictionary<string, string> Settings { get; set; } = ImmutableDictionary<string, string>.Empty;
        public int Calls
        {
            get; private set;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls++;
            return targetMethod?.Name switch
            {
                nameof(IOpenIddictApplicationManager.FindByClientIdAsync) => new ValueTask<object?>(this),
                nameof(IOpenIddictApplicationManager.GetSettingsAsync) => new ValueTask<ImmutableDictionary<string, string>>(Settings),
                _ => throw new InvalidOperationException("Unexpected application metadata operation.")
            };
        }
    }
}
