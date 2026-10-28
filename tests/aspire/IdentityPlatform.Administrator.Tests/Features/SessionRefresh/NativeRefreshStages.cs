using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Retains selected installed native handlers and filters while declaring the prevalidated-principal test seam explicitly.</summary>
internal static class NativeRefreshStages
{
    internal const string Issuer = "https://refresh-characterization.example.test/";
    private const string TokenPath = "connect/token";
    internal const string Client = "refresh-characterization-client";
    private static readonly OpenIddictServerHandlerDescriptor[] Descriptors =
    [
        OpenIddictServerHandlers.Protection.ValidateTokenEntry.Descriptor,
        OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry.Descriptor,
        OpenIddictServerHandlers.RedeemTokenEntry.Descriptor,
        OpenIddictServerHandlers.Revocation.RevokeToken.Descriptor
    ];

    /// <summary>Registers only native storage stages; no production options, routes, grant permissions or credentials are modified.</summary>
    internal static void Configure(IServiceCollection services, TimeSpan? leeway)
    {
        foreach (var descriptor in Descriptors)
        {
            services.Add(descriptor.ServiceDescriptor);
            foreach (var filter in descriptor.FilterTypes)
            {
                services.TryAddScoped(filter);
            }
        }
        services.AddOptions<OpenIddictServerOptions>().Configure<TimeProvider>((options, clock) =>
        {
            options.TimeProvider = clock;
            options.Issuer = new Uri(Issuer);
            options.RefreshTokenReuseLeeway = leeway;
            options.Handlers.Clear();
            options.Handlers.AddRange(Descriptors);
        });
        services.AddScoped<IOpenIddictServerFactory, OpenIddictServerFactory>();
        services.AddScoped<IOpenIddictServerDispatcher, OpenIddictServerDispatcher>();
    }

    /// <summary>Builds the original native authentication notification consumed by early refresh redemption, without claiming crypto validation.</summary>
    internal static async ValueTask<OpenIddictServerEvents.ProcessSignInContext> SignInAsync(ProtocolStorageScope scope, ClaimsPrincipal principal)
    {
        var transaction = await TransactionAsync(scope);
        transaction.SetProperty(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!,
            new OpenIddictServerEvents.ProcessAuthenticationContext(transaction) { RefreshTokenPrincipal = principal });
        return new(transaction)
        {
            Principal = principal
        };
    }

    /// <summary>Supplies exact native IDs from manager-created rows to the installed status/authorization stages.</summary>
    internal static async ValueTask<OpenIddictServerEvents.ValidateTokenContext> ValidateAsync(ProtocolStorageScope scope, ClaimsPrincipal principal)
    {
        var context = new OpenIddictServerEvents.ValidateTokenContext(await TransactionAsync(scope))
        {
            Principal = principal,
            TokenId = principal.GetTokenId(),
            AuthorizationId = principal.GetAuthorizationId()
        };
        await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
        return context;
    }

    /// <summary>Creates a real native transaction with fixed test metadata and the original request cancellation budget.</summary>
    internal static async ValueTask<OpenIddictServerTransaction> TransactionAsync(ProtocolStorageScope scope)
    {
        var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
        transaction.CancellationToken = scope.Owner.RequestToken;
        transaction.BaseUri = new Uri(Issuer);
        transaction.RequestUri = new Uri(Issuer + TokenPath);
        transaction.EndpointType = OpenIddictServerEndpointType.Token;
        transaction.Request = new OpenIddictRequest { ClientId = Client, GrantType = OpenIddictConstants.GrantTypes.RefreshToken };
        return transaction;
    }
}
