using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Separates an earlier committed code's cryptographic validity and persisted binding from current disabled-profile redemption denial.</summary>
internal static class NativeAuthorityCodeOracle
{
    /// <summary>Uses installed native crypto handlers and strict issuer/lifetime validation without treating disabled-profile eligibility as cryptographic invalidity.</summary>
    internal static async Task<ClaimsPrincipal> ReadAsync(AdministratorProtocolScenario scenario, string code)
    {
        var principal = await ReadProtectedPayloadAsync(scenario, code);
        await RequireRowAsync(scenario, principal);
        await RequireIneligibleAsync(scenario, code);
        return principal;
    }

    /// <summary>Resolves the actual opaque native reference to its stored protected payload before validating any JWT bytes.</summary>
    private static Task<ClaimsPrincipal> ReadProtectedPayloadAsync(AdministratorProtocolScenario scenario, string code) =>
        new ProtocolStorageRequest(scenario.Account.Journey.Clock, TestContext.Current.CancellationToken).RunAsync(
            scenario.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
                transaction.BaseUri = scenario.Issuer;
                var context = new OpenIddictServerEvents.ValidateTokenContext(transaction) { Token = code, CancellationToken = token };
                context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
                await new OpenIddictServerHandlers.Protection.ResolveTokenValidationParameters().HandleAsync(context);
                await new OpenIddictServerHandlers.Protection.ValidateReferenceTokenIdentifier(
                    scope.Services.GetRequiredService<IOpenIddictTokenManager>()).HandleAsync(context);
                Assert.False(context.IsRejected);
                Assert.True(context.IsReferenceToken);
                Assert.True(!string.IsNullOrEmpty(context.TokenId));
                await ValidatePayloadAsync(context);
                Assert.NotNull(context.Principal);
                Assert.True(context.Principal.GetTokenId() == context.TokenId);
                Assert.True(context.Principal.GetPresenters().SequenceEqual([AdministratorProtocolIdentity.ClientId]));
                return context.Principal;
            });

    /// <summary>Preserves native algorithms/type/key selection and adds strict issuer/lifetime checks for the independent protected-artifact oracle.</summary>
    private static async Task ValidatePayloadAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        context.TokenValidationParameters.ValidateIssuer = true;
        context.TokenValidationParameters.ValidateLifetime = true;
        context.TokenValidationParameters.RequireSignedTokens = true;
        context.TokenValidationParameters.RequireExpirationTime = true;
        context.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
        await new OpenIddictServerHandlers.Protection.ValidateIdentityModelToken().HandleAsync(context);
        Assert.False(context.IsRejected);
        Assert.True(context.TokenValidationResult?.IsValid == true);
        Assert.NotNull(context.Principal);
    }

    /// <summary>Requires the actual native row to retain the issued subject, application, authorization, code type and valid status after external disable.</summary>
    private static Task<bool> RequireRowAsync(AdministratorProtocolScenario scenario, ClaimsPrincipal principal) =>
        new ProtocolStorageRequest(scenario.Account.Journey.Clock, TestContext.Current.CancellationToken).RunAsync(
            scenario.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
                var row = await manager.FindByIdAsync(principal.GetTokenId()!, token);
                Assert.NotNull(row);
                Assert.Equal(OpenIddictConstants.Statuses.Valid, await manager.GetStatusAsync(row, token));
                Assert.Equal(OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, await manager.GetTypeAsync(row, token));
                Assert.True(await manager.GetSubjectAsync(row, token) == scenario.Account.ActorId);
                Assert.True(await manager.GetAuthorizationIdAsync(row, token) == principal.GetAuthorizationId());
                var apps = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
                var app = await apps.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, token);
                Assert.NotNull(app);
                Assert.True(await manager.GetApplicationIdAsync(row, token) == await apps.GetIdAsync(app, token));
                return true;
            });

    /// <summary>The unchanged complete native validation pipeline must still reject the otherwise valid code under a disabled current profile.</summary>
    private static Task<bool> RequireIneligibleAsync(AdministratorProtocolScenario scenario, string code) =>
        new ProtocolStorageRequest(scenario.Account.Journey.Clock, TestContext.Current.CancellationToken).RunAsync(
            scenario.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
                transaction.BaseUri = scenario.Issuer;
                transaction.EndpointType = OpenIddictServerEndpointType.Token;
                transaction.Request = new()
                {
                    ClientId = AdministratorProtocolIdentity.ClientId
                };
                var context = new OpenIddictServerEvents.ValidateTokenContext(transaction) { Token = code, CancellationToken = token };
                context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
                await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
                Assert.True(context.IsRejected);
                Assert.Equal(OpenIddictConstants.Errors.InvalidToken, context.Error);
                return true;
            });
}
