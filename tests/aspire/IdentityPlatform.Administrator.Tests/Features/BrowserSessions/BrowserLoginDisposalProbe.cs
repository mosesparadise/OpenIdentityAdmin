using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Arms one post-context-disposal observation in the local or native root, leaving genuine database work intact.</summary>
internal sealed class BrowserLoginDisposalProbe(bool native, Action complete) : SaveChangesInterceptor
{
    private const int ObservationOffset = 1000;
    private AdminSessionDbContext? saved;
    internal bool Armed
    {
        get; set;
    }
    internal int Calls
    {
        get; private set;
    }

    /// <summary>Constructs cleanup before the context so reverse scope disposal releases the real context first.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<BrowserLoginDisposalCleanup>();
        services.AddDbContext<AdminSessionDbContext>((provider, options) =>
        {
            options.AddInterceptors(this);
            var cleanup = provider.GetRequiredService<BrowserLoginDisposalCleanup>();
            var ambient = ProtocolStorageAmbient.Current;
            if (Armed && !native && ambient is null)
            {
                cleanup.Complete = () => Finish(saved!);
            }
        });
        if (native)
        {
            services.AddOpenIddict().AddClient().AddEventHandler<OpenIddict.Client.OpenIddictClientEvents.ProcessChallengeContext>(handler => handler
                .SetOrder(OpenIddict.Client.OpenIddictClientHandlers.SendPushedAuthorizationRequest.Descriptor.Order + ObservationOffset).UseInlineHandler(_ =>
                {
                    if (Armed)
                    {
                        var scope = ProtocolStorageAmbient.Current!.Scope;
                        scope.Services.GetRequiredService<BrowserLoginDisposalCleanup>().Complete = () => Finish((AdminSessionDbContext)scope.Owner.Database);
                    }
                    return ValueTask.CompletedTask;
                }));
        }
    }
    /// <summary>Retains the exact context only after its genuine save, so local post-disposal observations can prove release.</summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (Armed && !native)
        {
            saved = (AdminSessionDbContext)eventData.Context!;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Requires actual disposal of the exact context before advancing time or injecting cleanup failure.</summary>
    private void Finish(AdminSessionDbContext database)
    {
        Assert.Throws<ObjectDisposedException>(() => _ = database.Model);
        Calls++;
        Armed = false;
        complete();
    }
}

/// <summary>Invokes a test-only hook after later-created scoped contexts are disposed.</summary>
internal sealed class BrowserLoginDisposalCleanup : IAsyncDisposable
{
    internal Action? Complete
    {
        get; set;
    }

    /// <summary>Consumes the hook once and propagates its exact failure to the response owner.</summary>
    public ValueTask DisposeAsync()
    {
        var action = Complete;
        Complete = null;
        action?.Invoke();
        return ValueTask.CompletedTask;
    }
}
