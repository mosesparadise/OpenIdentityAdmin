using System.Security.Claims;
using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Tests actual native descriptor preparation and crypto, including fields that override principal claims or token metadata.</summary>
public sealed class ProtocolTokenPreparationTests
{
    private const string Issuer = "https://client.example/";
    private const string Nonce = "prepared-native-nonce";
    private const string Changed = "changed-crypto-input";
    private const string Header = "typ";
    private const int KeyBytes = 32;
    private const int LifetimeSeconds = 300;

    /// <summary>Names independent descriptor and crypto-engine substitution boundaries.</summary>
    public enum Mutation
    {
        Subject, SubjectNonce, Claims, Expires, IssuedAt, Issuer, NotBefore, Type, Audience, Audiences,
        Headers, InnerHeaders, Compression, KeyIdHeader, Signing, Encryption, Handler, Descriptor
    }

    /// <summary>Native preparation followed by real signing/encryption produces a decryptable, signature-valid state with original claims.</summary>
    [Fact]
    public async Task Given_ActualNativePreparation_When_GeneratingProtectedState_Then_OriginalClaimsValidate()
    {
        var context = Context();
        var proof = await PrepareAsync(context);
        proof.RequireReady(context.SecurityTokenDescriptor, context.SecurityTokenHandler);
        await new OpenIddictClientHandlers.Protection.GenerateIdentityModelToken().HandleAsync(context);
        proof.RequireReady(context.SecurityTokenDescriptor, context.SecurityTokenHandler);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(context.Token, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidateAudience = false,
            IssuerSigningKey = context.SigningCredentials!.Key,
            TokenDecryptionKey = context.EncryptionCredentials!.Key,
            ClockSkew = TimeSpan.Zero
        });
        Assert.True(result.IsValid);
        Assert.Equal(Nonce, result.ClaimsIdentity.FindFirst(OpenIddictConstants.Claims.Private.Nonce)?.Value);
    }

    /// <summary>Every late descriptor change fails before native signing, including overrides invisible in the original principal.</summary>
    [Theory]
    [InlineData(Mutation.Subject)]
    [InlineData(Mutation.SubjectNonce)]
    [InlineData(Mutation.Claims)]
    [InlineData(Mutation.Expires)]
    [InlineData(Mutation.IssuedAt)]
    [InlineData(Mutation.Issuer)]
    [InlineData(Mutation.NotBefore)]
    [InlineData(Mutation.Type)]
    [InlineData(Mutation.Audience)]
    [InlineData(Mutation.Audiences)]
    [InlineData(Mutation.Headers)]
    [InlineData(Mutation.InnerHeaders)]
    [InlineData(Mutation.Compression)]
    [InlineData(Mutation.KeyIdHeader)]
    [InlineData(Mutation.Signing)]
    [InlineData(Mutation.Encryption)]
    [InlineData(Mutation.Handler)]
    [InlineData(Mutation.Descriptor)]
    public async Task Given_PreparedDescriptor_When_CryptoInputsChange_Then_ReadyProofRejects(Mutation mutation)
    {
        var context = Context();
        var proof = await PrepareAsync(context);
        Change(context, mutation);
        Assert.Throws<InvalidOperationException>(() => proof.RequireReady(mutation == Mutation.Descriptor ? new() : context.SecurityTokenDescriptor, context.SecurityTokenHandler));
        Assert.Null(context.Token);
    }

    /// <summary>Native attachment cannot legitimize an attacker-preset header that overrides the eventual native token type.</summary>
    [Fact]
    public void Given_PresetDescriptor_When_StartingPreparation_Then_RejectsBeforeNativeAttachment()
    {
        var context = Context();
        context.SecurityTokenDescriptor.AdditionalHeaderClaims = new Dictionary<string, object> { [Header] = Changed };
        Assert.Throws<InvalidOperationException>(() => Proof(context));
    }

    /// <summary>Executes the installed credential, subject and metadata handlers without a substitute implementation.</summary>
    private static async Task<ProtocolTokenPreparation> PrepareAsync(OpenIddictClientEvents.GenerateTokenContext context)
    {
        var proof = Proof(context);
        await proof.AdvanceAsync(ProtocolTokenPreparation.Stage.Credentials, context.SecurityTokenDescriptor, () => context.SecurityTokenHandler,
            () => new OpenIddictClientHandlers.Protection.AttachSecurityCredentials().HandleAsync(context));
        await proof.AdvanceAsync(ProtocolTokenPreparation.Stage.Subject, context.SecurityTokenDescriptor, () => context.SecurityTokenHandler,
            () => new OpenIddictClientHandlers.Protection.AttachTokenSubject().HandleAsync(context));
        await proof.AdvanceAsync(ProtocolTokenPreparation.Stage.Metadata, context.SecurityTokenDescriptor, () => context.SecurityTokenHandler,
            () => new OpenIddictClientHandlers.Protection.AttachTokenMetadata().HandleAsync(context));
        return proof;
    }

    /// <summary>Captures exactly the configured handler and credential identities before native preparation.</summary>
    private static ProtocolTokenPreparation Proof(OpenIddictClientEvents.GenerateTokenContext context) =>
        new(context.SecurityTokenDescriptor, context.Options.JsonWebTokenHandler, typeof(JsonWebTokenHandler), encrypted: true,
            context.Options.SigningCredentials, context.Options.EncryptionCredentials);

    /// <summary>Supplies isolated test-only symmetric keys and bounded native state claims; no persisted or production credentials are used.</summary>
    private static OpenIddictClientEvents.GenerateTokenContext Context()
    {
        var options = new OpenIddictClientOptions();
        options.SigningCredentials.Add(new(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(KeyBytes)), SecurityAlgorithms.HmacSha256));
        options.EncryptionCredentials.Add(new(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(KeyBytes)),
            SecurityAlgorithms.Aes256KW, SecurityAlgorithms.Aes256CbcHmacSha512));
        return new(new OpenIddictClientTransaction { Options = options, Logger = NullLogger.Instance })
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity()).SetCreationDate(DateTimeOffset.UtcNow)
                .SetExpirationDate(DateTimeOffset.UtcNow.AddSeconds(LifetimeSeconds))
                .SetClaim(OpenIddictConstants.Claims.Private.Issuer, Issuer).SetClaim(OpenIddictConstants.Claims.Private.Nonce, Nonce),
            TokenType = OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken,
            TokenFormat = OpenIddictConstants.TokenFormats.Private.JsonWebToken
        };
    }

    /// <summary>Mutates the descriptor actually consumed by IdentityModel while leaving the original principal untouched.</summary>
    private static void Change(OpenIddictClientEvents.GenerateTokenContext context, Mutation mutation)
    {
        var descriptor = context.SecurityTokenDescriptor;
        Action mutate = mutation switch
        {
            Mutation.Subject => () => descriptor.Subject = descriptor.Subject.Clone(),
            Mutation.SubjectNonce => () => descriptor.Subject.AddClaim(new(OpenIddictConstants.Claims.Private.Nonce, Changed)),
            Mutation.Claims => () => descriptor.Claims[OpenIddictConstants.Claims.Private.Nonce] = Changed,
            Mutation.Expires => () => descriptor.Expires = DateTime.MaxValue,
            Mutation.IssuedAt => () => descriptor.IssuedAt = DateTime.MinValue,
            Mutation.Issuer => () => descriptor.Issuer = Changed,
            Mutation.NotBefore => () => descriptor.NotBefore = DateTime.MaxValue,
            Mutation.Type => () => descriptor.TokenType = Changed,
            Mutation.Audience => () => descriptor.Audience = Changed,
            Mutation.Audiences => () => descriptor.Audiences.Add(Changed),
            Mutation.Headers => () => descriptor.AdditionalHeaderClaims = new Dictionary<string, object> { [Header] = Changed },
            Mutation.InnerHeaders => () => descriptor.AdditionalInnerHeaderClaims = new Dictionary<string, object> { [Header] = Changed },
            Mutation.Compression => () => descriptor.CompressionAlgorithm = CompressionAlgorithms.Deflate,
            Mutation.KeyIdHeader => () => descriptor.IncludeKeyIdInHeader = !descriptor.IncludeKeyIdInHeader,
            Mutation.Signing => () => descriptor.SigningCredentials = new(descriptor.SigningCredentials.Key, descriptor.SigningCredentials.Algorithm),
            Mutation.Encryption => () => descriptor.EncryptingCredentials = null,
            Mutation.Handler => () => context.SecurityTokenHandler = new(),
            Mutation.Descriptor => () => { }
            ,
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        mutate();
    }
}
