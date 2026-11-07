using IdentityPlatform.AuthServer.Features.Protocol.Keys;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.KeyRotation.Activation;

/// <summary>Retains native late-key-failure timing evidence in an isolated component provider, separate from coordinated HTTP replay guarantees.</summary>
internal static class NativeKeyGenerationEvidence
{
    private const string Node = "auth";
    private const string PilotClient = "identityplatform-pilot";
    private const string TokenPath = "/connect/token";
    private const string RedemptionAdapter = "IdentityPlatform.AuthServer.Features.Protocol.PilotReplay.PilotRedemption";
    private const int UnsupportedIdentityLifetimeSeconds = 301;
    private const string MissingAccess = "The test did not reach failure after native access generation.";
    private const string MissingIdentity = "Generated access token has no native identity.";
    private const string ReturnedCredentials = "Rejected native pipeline returned credentials.";
    private const string RemovedEntry = "Late rejection removed an existing native token row.";
    private const string WrongAddedEntry = "Late ID rejection must leave exactly the generated access entry.";
    private const string WrongBinding = "Earlier access entry changed the original code's identity binding.";

    /// <summary>Uses a genuinely issued code, independently verifies native partial generation, then requires fresh real HTTPS recovery.</summary>
    internal static async Task RequireAsync(IssuerEnvironment host)
    {
        using var browser = host.NodeClient(Node, autoRedirect: true);
        var code = await ExchangeEvidence.IssueAsync(host, browser);
        var before = await ExchangeSnapshot.CaptureAsync(host);
        var generatedTokenId = await GenerateAsync(host, code);
        await RequirePersistedAsync(host, code, before, generatedTokenId);
        using var recovery = host.NodeClient(Node, autoRedirect: true);
        var fresh = await host.AuthorizeAsync(recovery);
        using var tokens = await DistributionEvidence.RedeemAsync(host, Node, fresh);
    }

    /// <summary>Changes only an independently constructed test provider before its first native dispatch; no running issuer options are modified.</summary>
    private static async Task<string> GenerateAsync(IssuerEnvironment host, ExchangeEvidence code)
    {
        using var runtime = host.RuntimeServices();
        var options = RequireNativeRedemption(runtime);
        await using var scope = runtime.CreateAsyncScope();
        _ = scope.ServiceProvider.GetRequiredService<OpenIddictServerHandlers.RedeemTokenEntry>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OpenIddictServerDispatcher>();
        var transaction = Transaction(host, code, options);
        return await RequireLateFailureAsync(dispatcher, transaction, code);
    }

    /// <summary>Restores exactly the imported native redemption slot, preserving filters, context and ordering before dispatcher resolution.</summary>
    private static OpenIddictServerOptions RequireNativeRedemption(IServiceProvider runtime)
    {
        var options = runtime.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;
        var adapter = Assert.Single(options.Handlers, value => value.ServiceDescriptor.ServiceType.FullName == RedemptionAdapter &&
            value.ServiceDescriptor.ServiceType.Assembly == typeof(ActiveKeyPolicy).Assembly);
        var native = OpenIddictServerHandlers.RedeemTokenEntry.Descriptor;
        Assert.Equal(native.FilterTypes, adapter.FilterTypes);
        Assert.Equal(native.Order, adapter.Order);
        Assert.Equal(native.ContextType, adapter.ContextType);
        Assert.DoesNotContain(options.Handlers, value => value.ServiceDescriptor.ServiceType == native.ServiceDescriptor.ServiceType);
        options.Handlers[options.Handlers.IndexOf(adapter)] = native;
        return options;
    }

    /// <summary>Constructs the native component transaction from the real code and original relying-party binding without synthesizing a principal.</summary>
    private static OpenIddictServerTransaction Transaction(IssuerEnvironment host, ExchangeEvidence code, OpenIddictServerOptions options) => new()
    {
        Options = options,
        Logger = NullLogger.Instance,
        CancellationToken = TestContext.Current.CancellationToken,
        BaseUri = host.Issuer,
        RequestUri = new Uri(host.Issuer, TokenPath),
        EndpointType = OpenIddictServerEndpointType.Token,
        Request = new OpenIddictRequest
        {
            ClientId = PilotClient,
            GrantType = GrantTypes.AuthorizationCode,
            Code = code.Flow.Code,
            CodeVerifier = code.Flow.Verifier,
            RedirectUri = host.RelyingParty.Callback
        },
        Response = new OpenIddictResponse()
    };

    /// <summary>Validates the actual code natively before the explicit unsupported principal lifetime reaches the later identity-token guard.</summary>
    private static async Task<string> RequireLateFailureAsync(OpenIddictServerDispatcher dispatcher, OpenIddictServerTransaction transaction,
        ExchangeEvidence code)
    {
        var validated = new ValidateTokenContext(transaction)
        {
            Token = code.Flow.Code,
            ValidTokenTypes = { TokenTypeIdentifiers.Private.AuthorizationCode },
            DisableAudienceValidation = true
        };
        await dispatcher.DispatchAsync(validated);
        Assert.False(validated.IsRejected);
        Assert.NotNull(validated.Principal);
        var authenticated = new ProcessAuthenticationContext(transaction) { AuthorizationCodePrincipal = validated.Principal };
        transaction.SetProperty(typeof(ProcessAuthenticationContext).FullName!, authenticated);
        var signIn = new ProcessSignInContext(transaction)
        {
            Principal = validated.Principal.Clone(_ => true).SetIdentityTokenLifetime(TimeSpan.FromSeconds(UnsupportedIdentityLifetimeSeconds))
        };
        await dispatcher.DispatchAsync(signIn);
        Assert.True(signIn.IsRejected && signIn.Error == Errors.ServerError && signIn.ErrorDescription == ActiveKeyPolicy.SafeDescription);
        Assert.False(string.IsNullOrEmpty(signIn.AccessToken), MissingAccess);
        var generated = new JsonWebToken(signIn.AccessToken);
        Assert.True(generated.TryGetClaim(Claims.Private.TokenId, out var tokenId), MissingIdentity);
        Assert.NotNull(transaction.Response);
        Assert.True(string.IsNullOrEmpty(signIn.IdentityToken) && string.IsNullOrEmpty(transaction.Response.AccessToken) &&
            string.IsNullOrEmpty(transaction.Response.IdToken), ReturnedCredentials);
        return tokenId.Value;
    }

    /// <summary>Reads fresh persisted rows after the component scope is disposed, retaining exact redemption, cardinality, status and identity bindings.</summary>
    private static async Task RequirePersistedAsync(IssuerEnvironment host, ExchangeEvidence code, ExchangeSnapshot before, string generatedTokenId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var token = TestContext.Current.CancellationToken;
        var entry = await manager.FindByReferenceIdAsync(code.Flow.Code, token);
        Assert.NotNull(entry);
        Assert.True(await manager.HasStatusAsync(entry, Statuses.Redeemed, token));
        var after = await ExchangeSnapshot.CaptureAsync(host);
        var added = after.TokenIds.Except(before.TokenIds, StringComparer.Ordinal).ToArray();
        Assert.True(before.TokenIds.IsSubsetOf(after.TokenIds), RemovedEntry);
        Assert.True(added.Length == 1 && added[0] == generatedTokenId, WrongAddedEntry);
        var access = await manager.FindByIdAsync(added[0], token);
        Assert.NotNull(access);
        Assert.True(await manager.GetTypeAsync(access, token) == TokenTypeIdentifiers.AccessToken &&
            await manager.HasStatusAsync(access, Statuses.Valid, token));
        Assert.True(await manager.GetSubjectAsync(access, token) == await manager.GetSubjectAsync(entry, token) &&
            await manager.GetApplicationIdAsync(access, token) == await manager.GetApplicationIdAsync(entry, token) &&
            await manager.GetAuthorizationIdAsync(access, token) == await manager.GetAuthorizationIdAsync(entry, token), WrongBinding);
    }
}
