using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Checks the compatibility wrapper's delegation boundary separately from actual cryptographic missing-key regressions.</summary>
public sealed class NativeClientStateValidationTests
{
    // Readable but deliberately unsigned synthetic input; validation is performed only by the test probe below.
    private static readonly string ReadableToken = string.Join(".", Base64UrlEncoder.Encode("{\"alg\":\"RS256\"}"),
        Base64UrlEncoder.Encode("{\"sub\":\"synthetic\"}"), Base64UrlEncoder.Encode("synthetic"));

    /// <summary>Only unresolved state receives an empty temporary registration; every path delegates once and restores identity even on exceptions.</summary>
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public async Task Given_NativeValidationContext_When_CompatibilityHandlerDelegates_Then_OnlyUnresolvedStateGetsAnEmptyTemporaryRegistration(bool state, bool registered, bool throws)
    {
        var original = registered ? new OpenIddictClientRegistration() : null;
        var context = new OpenIddictClientEvents.ValidateTokenContext(new OpenIddictClientTransaction { Registration = original!, Logger = NullLogger.Instance })
        {
            Token = ReadableToken,
            TokenValidationParameters = new()
        };
        context.ValidTokenTypes.Add(state ? OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken : OpenIddictConstants.TokenTypeIdentifiers.IdentityToken);
        var parameters = context.TokenValidationParameters;
        var expected = new NotSupportedException();
        var probe = new ValidationProbe(() => RequireRegistration(context, original, state && !registered), throws ? expected : null);
        context.SecurityTokenHandler = probe;
        var failure = await Record.ExceptionAsync(() => new NativeClientStateValidation(new()).HandleAsync(context).AsTask());
        Assert.Same(throws ? expected : null, failure);
        Assert.Equal(1, probe.Calls);
        Assert.Same(original, context.Registration);
        Assert.Same(parameters, context.TokenValidationParameters);
        Assert.Same(probe, context.SecurityTokenHandler);
        Assert.True(context.Token == ReadableToken);
        Assert.Equal(!throws, context.IsRejected);
        Assert.Null(context.Principal);
    }

    /// <summary>Ensures the temporary object cannot supply identity, signing trust, endpoints or remote metadata refresh.</summary>
    private static void RequireRegistration(OpenIddictClientEvents.ValidateTokenContext context, OpenIddictClientRegistration? original, bool temporary)
    {
        if (!temporary)
        {
            Assert.Same(original, context.Registration);
            return;
        }
        var registration = context.Registration;
        Assert.NotNull(registration);
        Assert.Null(registration.ConfigurationManager);
        Assert.Null(registration.Configuration);
        Assert.Null(registration.ConfigurationEndpoint);
        Assert.Null(registration.Issuer);
        Assert.Null(registration.ClientId);
        Assert.Null(registration.RegistrationId);
        Assert.Null(registration.RedirectUri);
        Assert.Null(registration.ClientSecret);
        Assert.Empty(registration.SigningCredentials);
        Assert.Empty(registration.EncryptionCredentials);
    }

    /// <summary>Observes the original native handler's one validation call; this probe is not cryptographic acceptance evidence.</summary>
    private sealed class ValidationProbe(Action observe, Exception? failure) : JsonWebTokenHandler
    {
        internal int Calls
        {
            get; private set;
        }

        /// <summary>Returns an ordinary native signature rejection or the exact injected exception without interpreting token bytes.</summary>
        public override Task<TokenValidationResult> ValidateTokenAsync(string token, TokenValidationParameters validationParameters)
        {
            Calls++;
            observe();
            return failure is null
                ? Task.FromResult(new TokenValidationResult { IsValid = false, Exception = new SecurityTokenInvalidSignatureException() })
                : Task.FromException<TokenValidationResult>(failure);
        }
    }
}
