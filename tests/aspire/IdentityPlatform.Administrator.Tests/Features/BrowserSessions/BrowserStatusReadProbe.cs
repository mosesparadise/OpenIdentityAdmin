using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Injects a bounded real-read or post-context-disposal fault without replacing provider persistence.</summary>
internal sealed class BrowserStatusReadProbe(bool readFailure, Action cleanup) : DbCommandInterceptor
{
    private const string Failure = "Synthetic status read failure.";
    private AdminSessionDbContext? observed;
    internal bool Armed
    {
        get; set;
    }
    internal int Calls
    {
        get; private set;
    }

    /// <summary>Creates cleanup before the real context so reverse disposal verifies that persistence was actually released.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<BrowserLoginDisposalCleanup>();
        services.AddDbContext<AdminSessionDbContext>((provider, options) =>
        {
            options.AddInterceptors(this);
            var lifetime = provider.GetRequiredService<BrowserLoginDisposalCleanup>();
            if (Armed && !readFailure)
            {
                lifetime.Complete = () =>
                {
                    Assert.NotNull(observed);
                    Assert.Throws<ObjectDisposedException>(() => _ = observed.Model);
                    Calls++;
                    Armed = false;
                    cleanup();
                };
            }
        });
    }

    /// <summary>Fails exactly one actual database read, or captures its context for a later cleanup-only observation.</summary>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (Armed)
        {
            observed = Assert.IsType<AdminSessionDbContext>(eventData.Context);
            if (readFailure)
            {
                Calls++;
                Armed = false;
                throw new InvalidOperationException(Failure);
            }
        }
        return ValueTask.FromResult(result);
    }
}
