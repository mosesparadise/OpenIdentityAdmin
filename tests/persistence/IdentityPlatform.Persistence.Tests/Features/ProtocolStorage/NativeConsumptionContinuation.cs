using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Observes committed native state from the next dispatcher slot before downstream rejection or remote work.</summary>
internal sealed class NativeConsumptionContinuation(IServiceScopeFactory factory)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>, IOpenIddictClientHandler<OpenIddictClientEvents.ProcessAuthenticationContext>
{
    private static readonly string[] ConsumedStatuses = [OpenIddictConstants.Statuses.Redeemed, OpenIddictConstants.Statuses.Revoked];
    /// <summary>Checks server consumption before later sign-in handlers can start issuance.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context) => ObserveAsync(context.Principal!);

    /// <summary>Checks client consumption before the actual native endpoint/error handlers run.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessAuthenticationContext context) => ObserveAsync(context.StateTokenPrincipal!);

    /// <summary>Uses another fresh reader and requires the consumption child lease already released after actual cleanup.</summary>
    private async ValueTask ObserveAsync(ClaimsPrincipal principal)
    {
        var parent = ProtocolStorageAmbient.Current!;
        Assert.True(parent.Scope.Owner.Children.IsIdle);
        Assert.Null(parent.Scope.Owner.Transaction);
        await ProtocolStorageChild.RunAsync(factory, async (scope, token) =>
        {
            var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
            var entry = await manager.FindByIdAsync(principal.GetTokenId()!, token);
            Assert.NotNull(entry);
            var status = await manager.GetStatusAsync(entry, token);
            Assert.Contains(status, ConsumedStatuses);
            return true;
        });
    }
}
