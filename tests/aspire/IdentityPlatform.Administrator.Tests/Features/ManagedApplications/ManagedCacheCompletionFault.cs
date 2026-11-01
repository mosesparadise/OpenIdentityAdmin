using System.Data.Common;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Fails the selected genuine managed cache or issuance child after actual provider commit or actual context disposal.</summary>
internal sealed class ManagedCacheCompletionFault(bool cleanup, bool issuance = false) : DbTransactionInterceptor
{
    private const string Failure = "Synthetic managed cache completion failure.";
    private PlatformDbContext? database;
    private Guid transactionId;
    internal bool Armed { get; set; } = true;
    internal Action? Advance
    {
        get; set;
    }
    internal int Commits
    {
        get; private set;
    }
    internal int Disposals
    {
        get; private set;
    }
    internal int Faults
    {
        get; private set;
    }

    /// <summary>Preserves reverse DI disposal so the sentinel observes the exact native context after it has really disposed.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<NativeClientCreationCleanup>();
        services.AddDbContext<PlatformDbContext>((provider, options) =>
        {
            _ = provider.GetRequiredService<NativeClientCreationCleanup>();
            options.AddInterceptors(this);
        });
        services.AddOpenIddict().AddServer(server => server.AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.Protection.AttachTokenPayload.Descriptor.Order + 1).UseInlineHandler(ObserveAsync)));
    }

    /// <summary>Captures only the native reference-cache generation after its real payload save, excluding administrator issuance.</summary>
    private ValueTask ObserveAsync(OpenIddictServerEvents.GenerateTokenContext context)
    {
        if (!Armed || (issuance ? ManagedIssuanceProof.Find(context.Transaction) is null : ManagedCacheProof.Find(context.Transaction) is null))
        {
            return ValueTask.CompletedTask;
        }
        var scope = ProtocolStorageAmbient.Current!.Scope;
        if (issuance && database is not null)
        {
            Assert.Same(database, scope.Owner.Database);
            Assert.Equal(transactionId, database.Database.CurrentTransaction!.TransactionId);
            return ValueTask.CompletedTask;
        }
        Assert.Null(database);
        database = Assert.IsType<PlatformDbContext>(scope.Owner.Database);
        transactionId = database.Database.CurrentTransaction!.TransactionId;
        scope.Services.GetRequiredService<NativeClientCreationCleanup>().Complete = AfterDisposal;
        return ValueTask.CompletedTask;
    }

    /// <summary>Rejects acknowledgement only after the exact provider transaction committed; resulting rows must remain despite withheld output.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(eventData.Context, database))
        {
            Assert.Equal(transactionId, eventData.TransactionId);
            Commits++;
            if (!cleanup && Advance is null)
            {
                Faults++;
                throw new InvalidOperationException(Failure);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>Proves real disposal before the cleanup fault, with no detached work or synthetic cleanup receipt.</summary>
    private void AfterDisposal()
    {
        Assert.IsType<ObjectDisposedException>(Record.Exception(() => _ = database!.Model));
        Disposals++;
        if (Advance is not null)
        {
            Assert.Equal(1, Commits);
            Faults++;
            Advance();
        }
        else if (cleanup)
        {
            Assert.Equal(1, Commits);
            Faults++;
            throw new InvalidOperationException(Failure);
        }
    }
}
