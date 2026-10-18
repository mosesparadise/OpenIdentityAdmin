using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Observes real native client assertion preparation/signing and rejects preset or substituted crypto inputs and outputs.</summary>
public sealed class NativeClientAssertionTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string Changed = "synthetic-substitution";
    private const string Client = "identity-admin-bff";
    private const string AlternateAudience = "https://issuer.example/connect/token";

    /// <summary>Names independent changes applied to the actual prepared assertion or descriptor, never a synthetic signed acceptance token.</summary>
    public enum Fault
    {
        PresetOutput, Subject, Audience, Identifier, Lifetime, DescriptorType, DescriptorClaims, DescriptorEncryption, Handler, Output
    }

    /// <summary>Actual native assertion output verifies with the public fixed registration key and carries the exact backend proof identity.</summary>
    [Fact]
    public async Task Given_FixedNativeAssertion_When_ObservedGenerationCompletes_Then_ActualSignatureAndPreparedClaimsMatch()
    {
        using var credentials = NativeClientProfileTests.Load(fixture);
        await using var provider = NativeClientCompositionTests.Create(credentials);
        var profile = provider.GetRequiredService<NativeClientProfile>();
        var context = await ContextAsync(provider);
        await NativeClientAssertion.RunAsync(context, profile, () => GenerateAsync(context, credentials));
        NativeClientAssertion.Find(context.Transaction)!.RequireOutput(context.Token, context.Principal);
        var token = new JsonWebTokenHandler().ReadJsonWebToken(context.Token);
        Assert.Equal("client-authentication+jwt", token.Typ);
        Assert.Equal("RS256", token.Alg);
        Assert.Equal(NativeClientProfileTests.KeyId, token.Kid);
        Assert.Equal(Client, token.Issuer);
        Assert.Equal(Client, token.Subject);
        Assert.Equal([NativeClientProfileTests.Issuer], token.Audiences);
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(context.Token, new TokenValidationParameters
        {
            ValidIssuer = Client,
            ValidAudience = NativeClientProfileTests.Issuer,
            IssuerSigningKey = profile.Registration.Key.CreateNativeKey(),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidTypes = [OpenIddictConstants.JsonWebTokenTypes.ClientAuthentication]
        });
        Assert.True(validation.IsValid, validation.Exception?.GetType().Name);
    }

    /// <summary>Every mutation is applied at the real stage and must prevent completed-output admission.</summary>
    [Theory]
    [InlineData(Fault.PresetOutput)]
    [InlineData(Fault.Subject)]
    [InlineData(Fault.Audience)]
    [InlineData(Fault.Identifier)]
    [InlineData(Fault.Lifetime)]
    [InlineData(Fault.DescriptorType)]
    [InlineData(Fault.DescriptorClaims)]
    [InlineData(Fault.DescriptorEncryption)]
    [InlineData(Fault.Handler)]
    [InlineData(Fault.Output)]
    public async Task Given_AlteredNativeAssertion_When_GenerationOrOutputIsObserved_Then_SubstitutionIsRejected(Fault fault)
    {
        using var credentials = NativeClientProfileTests.Load(fixture);
        await using var provider = NativeClientCompositionTests.Create(credentials);
        var context = await ContextAsync(provider);
        ChangePrepared(context, fault);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await NativeClientAssertion.RunAsync(context, provider.GetRequiredService<NativeClientProfile>(), () => GenerateAsync(context, credentials, fault));
            NativeClientAssertion.Find(context.Transaction)!.RequireOutput(fault == Fault.Output ? Changed : context.Token, context.Principal);
        });
    }

    /// <summary>Obtains the assertion principal from the real installed challenge preparation handler.</summary>
    private static async Task<OpenIddictClientEvents.GenerateTokenContext> ContextAsync(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var transaction = new OpenIddictClientTransaction { Options = options, Registration = Assert.Single(options.Registrations), Logger = NullLogger.Instance };
        var challenge = new OpenIddictClientEvents.ProcessChallengeContext(transaction) { ClientId = Client };
        await new OpenIddictClientHandlers.PrepareChallengeClientAssertionPrincipal().HandleAsync(challenge);
        return new(transaction)
        {
            Principal = challenge.ClientAssertionPrincipal!,
            TokenType = OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion,
            TokenFormat = OpenIddictConstants.TokenFormats.Private.JsonWebToken
        };
    }

    /// <summary>Preserves real native preparation and actual cryptographic handlers, inserting only one deliberately reached test mutation.</summary>
    private static async ValueTask GenerateAsync(OpenIddictClientEvents.GenerateTokenContext context, NativeClientCredentials credentials, Fault? fault = null)
    {
        await new NativeStateCredentials(new OpenIddictClientHandlers.Protection.AttachSecurityCredentials()).HandleAsync(context);
        await new NativeClientKeys(credentials).HandleAsync(context);
        await new NativeStateSubject(new OpenIddictClientHandlers.Protection.AttachTokenSubject()).HandleAsync(context);
        await new NativeStateMetadata(new OpenIddictClientHandlers.Protection.AttachTokenMetadata()).HandleAsync(context);
        switch (fault)
        {
            case Fault.DescriptorType:
                context.SecurityTokenDescriptor.TokenType = Changed;
                break;
            case Fault.DescriptorClaims:
                context.SecurityTokenDescriptor.Claims![OpenIddictConstants.Claims.Nonce] = Changed;
                break;
            case Fault.DescriptorEncryption:
                context.EncryptionCredentials = context.Options.EncryptionCredentials[0];
                break;
            case Fault.Handler:
                context.SecurityTokenHandler = new JsonWebTokenHandler();
                break;
        }
        await new NativeStateSigning(new OpenIddictClientHandlers.Protection.GenerateIdentityModelToken()).HandleAsync(context);
    }

    /// <summary>Changes one native prepared identity/time field before the observer starts, or leaves it for a later crypto-stage mutation.</summary>
    private static void ChangePrepared(OpenIddictClientEvents.GenerateTokenContext context, Fault fault)
    {
        const int OverlongSeconds = 61;
        switch (fault)
        {
            case Fault.PresetOutput:
                context.Token = Changed;
                break;
            case Fault.Subject:
                context.Principal.SetClaim(OpenIddictConstants.Claims.Subject, Changed);
                break;
            case Fault.Audience:
                context.Principal.SetAudiences(AlternateAudience);
                break;
            case Fault.Identifier:
                context.Principal.SetClaim(OpenIddictConstants.Claims.JwtId, string.Empty);
                break;
            case Fault.Lifetime:
                context.Principal.SetExpirationDate(context.Principal.GetCreationDate()!.Value.AddSeconds(OverlongSeconds));
                break;
        }
    }
}
