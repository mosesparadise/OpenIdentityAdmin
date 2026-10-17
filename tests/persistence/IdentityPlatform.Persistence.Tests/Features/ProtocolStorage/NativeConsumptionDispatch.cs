using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Invokes the installed native dispatcher with its original transaction and exact previously validated principal.</summary>
internal static class NativeConsumptionDispatch
{
    private const string Client = "identity-admin-bff";

    /// <summary>Chooses the owning native stack without substituting redemption or manager behavior.</summary>
    internal static ValueTask<bool> RunAsync(ProtocolStorageScope scope, ClaimsPrincipal principal) =>
        principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken) ? ClientAsync(scope, principal) : ServerAsync(scope, principal);

    /// <summary>Exercises the actual validation adapter, whose native replay path performs durable authorization-chain revocation.</summary>
    internal static async ValueTask<bool> ValidateAsync(ProtocolStorageScope scope, ClaimsPrincipal principal)
    {
        var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
        var context = new OpenIddictServerEvents.ValidateTokenContext(transaction)
        {
            Principal = principal,
            TokenId = principal.GetTokenId(),
            AuthorizationId = principal.GetAuthorizationId()
        };
        await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
        return !context.IsRejected;
    }

    /// <summary>Uses the installed library's creation handlers, including its deliberately null stored client-state subject.</summary>
    internal static async Task CreateAsync(ProtocolStorageScope scope, ClaimsPrincipal principal)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        if (principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken))
        {
            var transaction = await scope.Services.GetRequiredService<IOpenIddictClientFactory>().CreateTransactionAsync();
            await new OpenIddictClientHandlers.Protection.CreateTokenEntry(manager).HandleAsync(
                new OpenIddictClientEvents.GenerateTokenContext(transaction) { Principal = principal, TokenType = principal.GetTokenType()! });
            return;
        }
        var serverTransaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
        await new OpenIddictServerHandlers.Protection.CreateTokenEntry(scope.Services.GetRequiredService<IOpenIddictApplicationManager>(), manager)
            .HandleAsync(new OpenIddictServerEvents.GenerateTokenContext(serverTransaction)
            {
                Principal = principal,
                TokenType = principal.GetTokenType()!,
                ClientId = Client
            });
    }

    /// <summary>Stores the actual authentication notification consumed by native server early redemption.</summary>
    private static async ValueTask<bool> ServerAsync(ProtocolStorageScope scope, ClaimsPrincipal principal)
    {
        var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
        var requestToken = principal.HasTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken);
        transaction.EndpointType = requestToken ? OpenIddictServerEndpointType.Authorization : OpenIddictServerEndpointType.Token;
        transaction.Request = new OpenIddictRequest { ClientId = Client, GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode };
        var authentication = new OpenIddictServerEvents.ProcessAuthenticationContext(transaction)
        {
            AuthorizationCodePrincipal = requestToken ? null : principal,
            RequestTokenPrincipal = requestToken ? principal : null
        };
        transaction.SetProperty(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!, authentication);
        var context = new OpenIddictServerEvents.ProcessSignInContext(transaction) { Principal = principal };
        await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
        return !context.IsRejected;
    }

    /// <summary>Runs early native state consumption before registration resolution, correlation or remote transport.</summary>
    internal static async ValueTask<bool> ClientAsync(ProtocolStorageScope scope, ClaimsPrincipal principal, string? error = null)
    {
        var transaction = await scope.Services.GetRequiredService<IOpenIddictClientFactory>().CreateTransactionAsync();
        transaction.EndpointType = OpenIddictClientEndpointType.Redirection;
        transaction.Request = new OpenIddictRequest { [OpenIddictConstants.Parameters.Error] = error };
        var context = new OpenIddictClientEvents.ProcessAuthenticationContext(transaction)
        {
            StateTokenPrincipal = principal,
            ValidateStateToken = true,
            DisableStateTokenRedeeming = false
        };
        await scope.Services.GetRequiredService<IOpenIddictClientDispatcher>().DispatchAsync(context);
        Assert.Null(context.Registration);
        return !context.IsRejected;
    }
}
