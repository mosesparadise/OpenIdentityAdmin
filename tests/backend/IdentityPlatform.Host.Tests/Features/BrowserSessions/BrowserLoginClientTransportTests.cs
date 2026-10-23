using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Host.Tests.Features.AdministratorClient;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Separates native client parameter/state feasibility from the unchanged product B2 challenge allowlist.</summary>
public sealed class BrowserLoginClientTransportTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string NativeStateSubject = "synthetic-state-subject";
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Nonce = "native-browser-transport-nonce";
    private const string Forgery = "native-browser-transport-correlation";

    /// <summary>Actual native handlers copy exact fields into PAR and protect the original intent digest against later request substitution.</summary>
    [Fact]
    public async Task Given_CanonicalPair_When_NativeClientPreparesParAndProtectedState_Then_OriginalExpectationSurvives()
    {
        using var keys = NativeClientProfileTests.Load(fixture);
        await using var services = NativeClientCompositionTests.Create(keys);
        var context = Context(services);
        AddPair(context);
        await new OpenIddictClientHandlers.AttachCustomChallengeParameters().HandleAsync(context);
        await new OpenIddictClientHandlers.AttachPushedAuthorizationRequestParameters().HandleAsync(context);
        Assert.Equal(BrowserLoginBindingState.Present, BrowserLoginBinding.Read(context.PushedAuthorizationRequest!, out var binding));
        Assert.Equal(BrowserLoginBindingTests.Intent, binding!.Intent);
        Assert.Equal(BrowserLoginBindingTests.Completion, binding.Completion);
        await new OpenIddictClientHandlers.PrepareLoginStateTokenPrincipal().HandleAsync(context);
        var encoded = await NativeClientRetainedKeyTests.GenerateAsync(services, keys, context.StateTokenPrincipal!);
        context.Request.SetParameter(BrowserLoginBinding.IntentParameter, BrowserLoginBindingTests.Other);
        var state = await NativeClientRetainedKeyTests.ValidateAsync(context.Options, encoded);
        Assert.Equal(BrowserLoginBindingTests.Digest, BrowserSessionIntent.Read(state));
        Assert.Equal(BrowserLoginBindingState.Absent, BrowserLoginBinding.Read(state, out _));
        Assert.DoesNotContain(state.Claims, claim => claim.Value == BrowserLoginBindingTests.Completion);
    }

    /// <summary>Feasible native transport does not widen the production challenge boundary before durable B3 completion is implemented.</summary>
    [Fact]
    public void Given_OrdinaryB2Challenge_When_B3PairIsAdded_Then_ExistingProductPolicyRejectsIt()
    {
        using var keys = NativeClientProfileTests.Load(fixture);
        using var services = NativeClientCompositionTests.Create(keys);
        var context = Context(services);
        var profile = services.GetRequiredService<NativeClientProfile>();
        NativeClientChallenge.Require(context, profile);
        AddPair(context);
        Assert.Throws<InvalidOperationException>(() => NativeClientChallenge.Require(context, profile));
        context.Parameters.Remove(BrowserLoginBinding.IntentParameter);
        context.Parameters.Remove(BrowserLoginBinding.CompletionParameter);
        NativeClientChallenge.Require(context, profile);
    }

    /// <summary>Supplies a complete otherwise valid challenge so pair rejection cannot be credited to an unrelated missing binding.</summary>
    private static OpenIddictClientEvents.ProcessChallengeContext Context(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var profile = services.GetRequiredService<NativeClientProfile>();
        var context = new OpenIddictClientEvents.ProcessChallengeContext(new OpenIddictClientTransaction
        {
            Options = options,
            Registration = Assert.Single(options.Registrations),
            Logger = NullLogger.Instance,
            Configuration = NativeClientProfileTests.Configuration(profile),
            Request = new()
        })
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity([new(OpenIddictConstants.Claims.Subject, NativeStateSubject),
                new(BrowserSessionIntent.ClaimType, BrowserLoginBindingTests.Digest)])),
            ClientId = AdministratorProtocolIdentity.ClientId,
            RedirectUri = profile.Callback.AbsoluteUri,
            GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode,
            ResponseType = OpenIddictConstants.ResponseTypes.Code,
            ResponseMode = OpenIddictConstants.ResponseModes.Query,
            CodeChallengeMethod = OpenIddictConstants.CodeChallengeMethods.Sha256,
            CodeVerifier = Verifier,
            CodeChallenge = NativeClientChallenge.Hash(Verifier),
            Nonce = Nonce,
            RequestForgeryProtection = Forgery
        };
        context.Scopes.UnionWith([OpenIddictConstants.Scopes.OpenId, AdministratorProtocolIdentity.Scope]);
        context.Parameters.Add(OpenIddictConstants.Parameters.Prompt, OpenIddictConstants.PromptValues.None);
        return context;
    }

    /// <summary>Adds only the synthetic canonical pair and never supplies a completion receipt or source-session authority.</summary>
    private static void AddPair(OpenIddictClientEvents.ProcessChallengeContext context)
    {
        context.Parameters.Add(BrowserLoginBinding.IntentParameter, BrowserLoginBindingTests.Intent);
        context.Parameters.Add(BrowserLoginBinding.CompletionParameter, BrowserLoginBindingTests.Completion);
    }
}
