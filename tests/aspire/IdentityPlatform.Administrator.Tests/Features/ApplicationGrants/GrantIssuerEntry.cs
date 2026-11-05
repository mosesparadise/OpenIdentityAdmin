using System.Security.Claims;
using IdentityPlatform.Administrator.Tests.Features.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Isolates actual library entry checks over genuine issued-code identities from separate cryptographic, revision and audience checks.</summary>
internal sealed class GrantIssuerEntry(ClaimsPrincipal token, ClaimsPrincipal authorization)
{
    private const string Valid = "valid";

    /// <summary>Captures the genuine issuer-created code and authorization identities without constructing a signed credential or altering persisted state.</summary>
    internal static async Task<GrantIssuerEntry> ReadAsync(ManagedCacheTestContext test)
    {
        var snapshot = await test.SnapshotAsync(applicationOnly: true);
        var interaction = Assert.Single(snapshot.Interactions);
        var row = Assert.Single(snapshot.Tokens, row => row.Id == interaction.CodeId);
        Assert.Equal(Valid, row.Status);
        Assert.Equal(Valid, row.Authorization!.Status);
        Assert.NotNull(row.ExpirationDate);
        var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode)
            .SetExpirationDate(row.ExpirationDate);
        return new(principal.Clone().SetTokenId(row.Id), principal.Clone().SetAuthorizationId(row.Authorization.Id));
    }

    /// <summary>Uses separate owned native requests for each handler so token and authorization outcomes cannot mask one another.</summary>
    internal async Task RequireAsync(IServiceProvider root, TimeProvider clock, bool allowed)
    {
        Assert.True(token.GetExpirationDate() > clock.GetUtcNow());
        Assert.Equal(allowed, await AllowsAsync(root, clock, token, false));
        Assert.Equal(allowed, await AllowsAsync(root, clock, authorization, true));
    }

    /// <summary>Runs one installed native handler through real owned provider storage, with actual cleanup before reporting its outcome.</summary>
    private static async Task<bool> AllowsAsync(IServiceProvider root, TimeProvider clock, ClaimsPrincipal principal, bool authorization)
    {
        var request = new ProtocolStorageRequest(clock, TestContext.Current.CancellationToken);
        try
        {
            return await request.RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                var context = new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction
                {
                    Logger = NullLogger.Instance,
                    Options = new OpenIddictServerOptions { TimeProvider = clock }
                })
                {
                    Principal = principal.Clone(),
                    TokenId = principal.GetTokenId(),
                    AuthorizationId = principal.GetAuthorizationId(),
                    CancellationToken = token
                };
                if (authorization)
                {
                    await new OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry(scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>()).HandleAsync(context);
                }
                else
                {
                    await new OpenIddictServerHandlers.Protection.ValidateTokenEntry(scope.Services.GetRequiredService<IOpenIddictTokenManager>()).HandleAsync(context);
                }
                if (context.IsRejected)
                {
                    Assert.Equal(OpenIddictConstants.Errors.InvalidToken, context.Error);
                }
                return !context.IsRejected;
            });
        }
        finally { await request.DrainAsync(); }
    }
}
