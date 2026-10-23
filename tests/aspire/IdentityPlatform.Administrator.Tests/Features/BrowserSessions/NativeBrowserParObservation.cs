using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Observes a real persisted native PAR after payload completion; it never manufactures output or bypasses a native descriptor.</summary>
internal sealed class NativeBrowserParObservation
{
    private const int AfterNativePayload = 1;
    internal bool Armed
    {
        get; set;
    }
    internal int Reached
    {
        get; private set;
    }
    internal string Identifier { get; private set; } = string.Empty;
    internal Action<PlatformDbContext>? AfterPersisted
    {
        get; set;
    }

    /// <summary>Adds only an observational callback after the installed native payload handler.</summary>
    internal void Configure(IServiceCollection services) => services.AddOpenIddict().AddServer()
        .AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.Protection.AttachTokenPayload.Descriptor.Order + AfterNativePayload)
            .UseInlineHandler(ObserveAsync));

    /// <summary>Reads the native row through the actual owning transaction before attachment audit, then optionally arms the existing exact-context commit gate.</summary>
    private async ValueTask ObserveAsync(OpenIddictServerEvents.GenerateTokenContext context)
    {
        if (!Armed || context.EndpointType != OpenIddictServerEndpointType.PushedAuthorization ||
            context.TokenType != OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken)
        {
            return;
        }
        Assert.Equal(0, Reached);
        var scope = ProtocolStorageAmbient.Current!.Scope;
        var database = Assert.IsType<PlatformDbContext>(scope.Owner.Database);
        Assert.NotNull(database.Database.CurrentTransaction);
        Assert.False(string.IsNullOrEmpty(context.Token));
        var identifier = context.Principal.GetTokenId();
        Assert.False(string.IsNullOrEmpty(identifier));
        var persisted = await scope.Owner.RunAsync(current => new ValueTask<bool>(database.Set<OpenIddictEntityFrameworkCoreToken>()
            .AsNoTracking().AnyAsync(row => row.Id == identifier && row.Type == OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken &&
                row.Status == OpenIddictConstants.Statuses.Valid && row.Payload != null && row.Payload != string.Empty, current)), context.CancellationToken);
        Assert.True(persisted);
        Identifier = identifier!;
        Reached++;
        AfterPersisted?.Invoke(database);
    }
}
