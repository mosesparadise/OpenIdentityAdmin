using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Exercises actual native protected state before registration resolution when a retained signing key is present or absent.</summary>
public sealed class NativeClientMissingStateKeyTests
{
    private const string PreviousSigning = "ClientCredentials:StateSigning:Previous:";
    private const string Section = "ClientCredentials";
    private const string MissingKeyUri = "https://documentation.openiddict.com/errors/ID2090";
    private const string PreviousEncryption = "ClientCredentials:StateEncryption:Previous:";

    /// <summary>A missing signer must produce native cryptographic rejection without inventing a resolved registration or throwing during refresh lookup.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_StateBeforeRegistrationResolution_When_RetainedSignerIsMissing_Then_NativeValidationRejectsWithoutRegistrationRefresh(bool retain)
    {
        using var material = new NativeClientCredentialFixture();
        string token;
        using (var original = NativeClientProfileTests.Load(material))
        {
            await using var services = NativeClientCompositionTests.Create(original);
            token = await NativeClientRetainedKeyTests.GenerateAsync(services, original);
        }
        var settings = material.RotatedSettings().Where(pair => retain || !pair.Key.StartsWith(PreviousSigning, StringComparison.OrdinalIgnoreCase));
        using var replacement = NativeClientCredentials.Load(new ConfigurationBuilder().AddInMemoryCollection(settings).Build().GetSection(Section), DateTimeOffset.UtcNow);
        await using var restarted = NativeClientCompositionTests.Create(replacement);
        var options = restarted.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var context = new OpenIddictClientEvents.ValidateTokenContext(new OpenIddictClientTransaction { Options = options, Logger = NullLogger.Instance }) { Token = token };
        context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken);
        Assert.Null(context.Registration);
        await new OpenIddictClientHandlers.Protection.ResolveTokenValidationParameters().HandleAsync(context);
        var result = await context.SecurityTokenHandler.ValidateTokenAsync(token, context.TokenValidationParameters);
        Assert.Equal(retain, result.IsValid);
        if (!retain)
        {
            Assert.IsType<SecurityTokenSignatureKeyNotFoundException>(result.Exception);
        }
        var descriptor = options.Handlers.Single(value => value.ContextType == typeof(OpenIddictClientEvents.ValidateTokenContext) &&
            value.Order == OpenIddictClientHandlers.Protection.ValidateIdentityModelToken.Descriptor.Order);
        await ((IOpenIddictClientHandler<OpenIddictClientEvents.ValidateTokenContext>)restarted.GetRequiredService(descriptor.ServiceDescriptor.ServiceType)).HandleAsync(context);
        Assert.Null(context.Registration);
        Assert.Equal(!retain, context.IsRejected);
        Assert.Equal(retain, context.Principal is not null);
        if (!retain)
        {
            Assert.Equal(OpenIddictConstants.Errors.InvalidToken, context.Error);
            Assert.Equal(MissingKeyUri, context.ErrorUri);
        }
    }

    /// <summary>Keeping the old signer cannot rescue an encrypted state token when its old decryptor is absent.</summary>
    [Fact]
    public async Task Given_OnlyRetainedStateSigner_When_OldCiphertextIsValidated_Then_NativeDecryptionStillRejects()
    {
        using var material = new NativeClientCredentialFixture();
        string token;
        using (var original = NativeClientProfileTests.Load(material))
        {
            await using var services = NativeClientCompositionTests.Create(original);
            token = await NativeClientRetainedKeyTests.GenerateAsync(services, original);
        }
        var settings = material.RotatedSettings().Where(pair => !pair.Key.StartsWith(PreviousEncryption, StringComparison.OrdinalIgnoreCase));
        using var replacement = NativeClientCredentials.Load(new ConfigurationBuilder().AddInMemoryCollection(settings).Build().GetSection(Section), DateTimeOffset.UtcNow);
        await using var restarted = NativeClientCompositionTests.Create(replacement);
        var options = restarted.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var context = new OpenIddictClientEvents.ValidateTokenContext(new OpenIddictClientTransaction { Options = options, Logger = NullLogger.Instance }) { Token = token };
        context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken);
        await new OpenIddictClientHandlers.Protection.ResolveTokenValidationParameters().HandleAsync(context);
        var descriptor = options.Handlers.Single(value => value.ContextType == typeof(OpenIddictClientEvents.ValidateTokenContext) &&
            value.Order == OpenIddictClientHandlers.Protection.ValidateIdentityModelToken.Descriptor.Order);
        await ((IOpenIddictClientHandler<OpenIddictClientEvents.ValidateTokenContext>)restarted.GetRequiredService(descriptor.ServiceDescriptor.ServiceType)).HandleAsync(context);
        Assert.True(context.IsRejected);
        Assert.Equal(OpenIddictConstants.Errors.InvalidToken, context.Error);
        Assert.Null(context.Principal);
        Assert.Null(context.Registration);
    }
}
