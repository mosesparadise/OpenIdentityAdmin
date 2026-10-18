using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Exercises the real native authentication dispatcher before crypto parsing without opening a database.</summary>
public sealed class AdministratorAssertionIntegrationTests(AdministratorRegistrationFixture fixture) : IClassFixture<AdministratorRegistrationFixture>
{
    private const string Issuer = "https://assertion-guard.example.test/";
    private const string Callback = "https://assertion-guard.example.test/callback";
    private const string AuthorizePath = "/authorize";
    private const string TokenPath = "/token";
    private const string InvalidCode = "unused-invalid-code";
    private const string WrongMethod = "wrong-assertion-method";
    private const int MaximumLength = 8192;

    /// <summary>Native dispatch rejects oversized, non-ASCII or wrong-method assertions before any token parser or application store can run.</summary>
    [Theory]
    [InlineData(MaximumLength + 1, false, false)]
    [InlineData(MaximumLength, true, false)]
    [InlineData(MaximumLength, false, true)]
    public async Task Given_InvalidAssertion_When_NativeAuthenticationStarts_Then_RejectsBeforeParsing(int length, bool nonAscii, bool wrongMethod)
    {
        var parsed = false;
        await using var storage = CreateStorage(() => parsed = true);
        var assertion = new string(nonAscii ? '\u00e9' : 'a', length);
        var context = await AuthenticateAsync(storage, assertion, wrongMethod);
        Assert.Equal(OpenIddictConstants.Errors.InvalidClient, context.Error);
        Assert.False(parsed);
    }

    /// <summary>The exact ASCII byte ceiling reaches the deliberate native parser sentinel, proving the negative controls do not always reject.</summary>
    [Fact]
    public async Task Given_ExactAssertionBound_When_NativeAuthenticationStarts_Then_ParserBoundaryIsReached()
    {
        var parsed = false;
        await using var storage = CreateStorage(() => parsed = true);
        var context = await AuthenticateAsync(storage, new string('a', MaximumLength), false);
        Assert.True(parsed);
        Assert.Equal(OpenIddictConstants.Errors.InvalidClient, context.Error);
    }

    /// <summary>Installs actual default authentication handlers and a pre-crypto rejection sentinel in the real nested validation dispatcher.</summary>
    private StorageRequestProbe CreateStorage(Action parsing) => new(services =>
    {
        services.AddSingleton(AdministratorRegistrationPolicy.Create(Callback, fixture.Key)!);
        services.AddOpenIddict().AddCore(core => core.UseEntityFrameworkCore());
        var builder = services.AddOpenIddict().AddServer().SetIssuer(new Uri(Issuer)).SetAuthorizationEndpointUris(AuthorizePath)
            .SetTokenEndpointUris(TokenPath).AllowAuthorizationCodeFlow().AddEphemeralSigningKey().AddEphemeralEncryptionKey().AddAdministratorAssertions();
        builder.UseAspNetCore();
        builder.AddEventHandler<OpenIddictServerEvents.ValidateTokenContext>(handler => handler.SetOrder(int.MinValue).UseInlineHandler(context =>
        {
            parsing();
            context.Reject(OpenIddictConstants.Errors.InvalidClient);
            return ValueTask.CompletedTask;
        }));
        services.AddOwnedServerDispatcher();
    }, new ControlledProbeClock());

    /// <summary>Uses the installed factory and native token-endpoint authentication demand; no prevalidated claims are injected.</summary>
    private static Task<OpenIddictServerEvents.ProcessAuthenticationContext> AuthenticateAsync(StorageRequestProbe storage, string assertion, bool wrongMethod) =>
        new ProtocolStorageRequest(storage.Clock, TestContext.Current.CancellationToken).RunAsync(storage.Factory, async (scope, cancellation) =>
        {
            var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
            transaction.EndpointType = OpenIddictServerEndpointType.Token;
            transaction.Request = new OpenIddictRequest
            {
                ClientId = AdministratorProtocolIdentity.ClientId,
                ClientAssertion = assertion,
                ClientAssertionType = wrongMethod ? WrongMethod : OpenIddictConstants.ClientAssertionTypes.JwtBearer,
                GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode,
                Code = InvalidCode
            };
            var context = new OpenIddictServerEvents.ProcessAuthenticationContext(transaction) { CancellationToken = cancellation };
            await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
            return context;
        });
}
