using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Injects callback mutations only after a validated native state reaches its actual durable consumption child.</summary>
internal sealed class NativeClientCallbackFault
{
    private const string Changed = "synthetic-callback-substitution";
    internal enum Kind
    {
        None, ReadClone, ReadTokenId, ReadVerifier, CleanupClone, CleanupTokenId, CleanupVerifier
    }
    internal Kind Mode
    {
        get; set;
    }
    internal OpenIddictClientEvents.ProcessAuthenticationContext? Context
    {
        get; set;
    }
    internal int Reads
    {
        get; set;
    }
    internal int Mutations
    {
        get; set;
    }

    /// <summary>Installs test-only observation before the actual native redemption slot without replacing production handlers or managers.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddSingleton(this).AddScoped<NativeClientCallbackCleanup>().AddScoped<NativeClientCallbackRead>();
        services.AddDbContext<AdminSessionDbContext>((provider, options) => options.AddInterceptors(provider.GetRequiredService<NativeClientCallbackRead>()));
        services.AddOpenIddict().AddClient().AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(handler =>
            handler.SetOrder(OpenIddictClientHandlers.RedeemStateTokenEntry.Descriptor.Order - 1).UseInlineHandler(context =>
            {
                if (Mode != Kind.None)
                {
                    Context = context;
                }
                return ValueTask.CompletedTask;
            }));
    }

    /// <summary>Changes only the validated continuation; original state rows and protected payloads remain untouched by the injection.</summary>
    internal void Mutate()
    {
        var context = Context ?? throw new InvalidOperationException();
        Mutations++;
        switch (Mode)
        {
            case Kind.ReadClone:
            case Kind.CleanupClone:
                context.StateTokenPrincipal = context.StateTokenPrincipal!.Clone(_ => true);
                break;
            case Kind.ReadTokenId:
            case Kind.CleanupTokenId:
                context.StateTokenPrincipal!.SetTokenId(Changed);
                break;
            case Kind.ReadVerifier:
            case Kind.CleanupVerifier:
                context.StateTokenPrincipal!.SetClaim(OpenIddictConstants.Claims.Private.CodeVerifier, Changed);
                break;
            default:
                throw new InvalidOperationException();
        }
    }
}

/// <summary>Observes the first real asynchronous native-token read inside the selected callback's fresh consumption child.</summary>
internal sealed class NativeClientCallbackRead(NativeClientCallbackFault fault, NativeClientCallbackCleanup cleanup) : DbCommandInterceptor
{
    private const string Select = "SELECT";
    private const string Tokens = "OpenIddictTokens";

    /// <summary>Returns the provider's actual reader after the selected awaited mutation, or arms only this child's eventual cleanup.</summary>
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (fault.Mode != NativeClientCallbackFault.Kind.None && fault.Context is not null && fault.Reads == 0 &&
            command.CommandText.TrimStart().StartsWith(Select, StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains(Tokens, StringComparison.Ordinal))
        {
            Assert.NotNull(eventData.Context!.Database.CurrentTransaction);
            fault.Reads++;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (fault.Mode >= NativeClientCallbackFault.Kind.CleanupClone)
            {
                cleanup.Armed = true;
            }
            else
            {
                fault.Mutate();
            }
        }
        return result;
    }
}

/// <summary>Mutates only the actual consumption child after its commit and context disposal, before its caller can admit a receipt.</summary>
internal sealed class NativeClientCallbackCleanup(NativeClientCallbackFault fault) : IAsyncDisposable
{
    internal bool Armed
    {
        get; set;
    }

    /// <summary>Runs once through actual async DI disposal; no detached continuation or direct production callback is substituted.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Armed)
        {
            Armed = false;
            await Task.Yield();
            fault.Mutate();
        }
    }
}
