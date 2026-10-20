using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Observes one real state-generation child and injects a single failure after its provider commit or context disposal.</summary>
internal sealed class NativeClientCreationFault(bool cleanup) : DbTransactionInterceptor
{
    private const string FailureMessage = "Synthetic native state completion failure.";
    internal bool Armed
    {
        get; set;
    }
    internal Exception Failure { get; } = new InvalidOperationException(FailureMessage);
    internal string State
    {
        get; private set;
    } = string.Empty;
    internal string Identifier
    {
        get; private set;
    } = string.Empty;
    internal int Commits
    {
        get; private set;
    }
    internal int Faults
    {
        get; private set;
    }
    internal int Disposals
    {
        get; private set;
    }
    private AdminSessionDbContext? database;
    private Guid transactionId;

    /// <summary>Captures the sentinel before constructing each context, so reverse DI disposal releases that context before the sentinel may throw.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<NativeClientCreationCleanup>();
        services.AddDbContext<AdminSessionDbContext>((provider, options) =>
        {
            _ = provider.GetRequiredService<NativeClientCreationCleanup>();
            options.AddInterceptors(this);
        });
        services.AddOpenIddict().AddClient().AddEventHandler<OpenIddictClientEvents.GenerateTokenContext>(builder =>
            builder.SetOrder(OpenIddictClientHandlers.Protection.AttachTokenPayload.Descriptor.Order + 1)
                .UseInlineHandler(ObserveAsync));
    }

    /// <summary>Captures only a genuine reference-state generation after native payload persistence, including its exact child and transaction.</summary>
    private ValueTask ObserveAsync(OpenIddictClientEvents.GenerateTokenContext context)
    {
        if (!Armed || context.TokenType != OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken)
        {
            return ValueTask.CompletedTask;
        }
        Assert.Null(database);
        var scope = ProtocolStorageAmbient.Current!.Scope;
        database = Assert.IsType<AdminSessionDbContext>(scope.Owner.Database);
        transactionId = database.Database.CurrentTransaction!.TransactionId;
        Assert.False(string.IsNullOrEmpty(context.Token));
        Assert.False(string.IsNullOrEmpty(context.Principal.GetTokenId()));
        State = context.Token;
        Identifier = context.Principal.GetTokenId()!;
        scope.Services.GetRequiredService<NativeClientCreationCleanup>().Complete = AfterDisposal;
        return ValueTask.CompletedTask;
    }

    /// <summary>Requires the native provider callback on the exact observed transaction, never a pre-commit substitute or unrelated transaction.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Armed && ReferenceEquals(eventData.Context, database))
        {
            Assert.Equal(transactionId, eventData.TransactionId);
            Commits++;
            Assert.Equal(1, Commits);
            if (!cleanup)
            {
                Faults++;
                throw Failure;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>Proves the captured native context is disposed before injecting cleanup failure; other request scopes remain inert.</summary>
    private void AfterDisposal()
    {
        var disposed = Record.Exception(() => _ = database!.Model);
        Assert.IsType<ObjectDisposedException>(disposed);
        Disposals++;
        Assert.Equal(1, Disposals);
        if (Armed && cleanup)
        {
            Assert.Equal(1, Commits);
            Faults++;
            throw Failure;
        }
    }

    /// <summary>Requires every aggregated leaf to be the original injected exception, so unrelated cleanup failures cannot pass.</summary>
    internal bool Contains(Exception? error) => ReferenceEquals(error, Failure) ||
        (error is AggregateException aggregate ? aggregate.InnerExceptions.Count != 0 && aggregate.InnerExceptions.All(Contains) : error?.InnerException is { } inner && Contains(inner));
}

/// <summary>Runs an inert-by-default completion hook after the later-constructed native context has been disposed.</summary>
internal sealed class NativeClientCreationCleanup : IAsyncDisposable
{
    internal Action? Complete
    {
        get; set;
    }

    /// <summary>Consumes the exact generation child's hook once without affecting unrelated scopes or retaining a live context.</summary>
    public ValueTask DisposeAsync()
    {
        var complete = Complete;
        Complete = null;
        complete?.Invoke();
        return ValueTask.CompletedTask;
    }
}
