using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Names independently observed persistence, commit-observation and actual scope-disposal failure boundaries.</summary>
public enum PilotGenerationFailure
{
    AfterInsert, CommitAcknowledgement, Disposal
}

/// <summary>Injects one failure only after the real native access INSERT identifies its exact generation context and transaction.</summary>
internal sealed class PilotGenerationFault(PilotGenerationFailure mode) : DbTransactionInterceptor
{
    internal const string StagedHeader = "X-Pilot-Staged-Generation";
    private const string StagedValue = "synthetic-unpublished-output";
    private const string Pilot = "identityplatform-pilot";
    private const string FailureMessage = "Synthetic pilot generation boundary failure.";
    private DbContext? database;
    private DbTransaction? transaction;
    private Guid? transactionId;
    internal bool Armed
    {
        get; set;
    }
    internal string AccessId { get; private set; } = null!;
    internal int Inserts
    {
        get; private set;
    }
    internal int Saves
    {
        get; private set;
    }
    internal int CommitAttempts
    {
        get; private set;
    }
    internal int Commits
    {
        get; private set;
    }
    internal int Disposals
    {
        get; private set;
    }
    internal int Failures
    {
        get; private set;
    }
    internal Func<Task>? RequireConsumed
    {
        get; set;
    }
    internal Exception Failure { get; } = new InvalidOperationException(FailureMessage);

    /// <summary>Constructs the disposal sentinel before its native context so reverse disposal proves the context is already gone.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<PilotGenerationCleanup>();
        services.AddDbContext<PlatformDbContext>((provider, options) => options.AddInterceptors(this,
            new PilotGenerationSaveObservation(this, provider.GetRequiredService<PilotGenerationCleanup>())));
        services.AddOpenIddict().AddServer().AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder => builder
            .SetOrder(OpenIddictServerHandlers.RestoreInternalClaims.Descriptor.Order + 1).UseInlineHandler(context =>
            {
                if (Armed && context.ClientId == Pilot && context.EndpointType == OpenIddictServerEndpointType.Token && context.Request.IsAuthorizationCodeGrantType())
                {
                    context.Transaction.GetHttpRequest()!.HttpContext.Response.Headers[StagedHeader] = StagedValue;
                }
                return ValueTask.CompletedTask;
            }));
    }

    /// <summary>Captures the exact native generation transaction before its actual INSERT, never a validation or consumption transaction.</summary>
    internal void Begin(DbContext current, PilotGenerationCleanup cleanup)
    {
        Assert.Null(database);
        database = current;
        var owned = current.Database.CurrentTransaction;
        Assert.NotNull(owned);
        transaction = owned.GetDbTransaction();
        transactionId = owned.TransactionId;
        cleanup.Complete = () => AfterDisposal(current);
    }

    /// <summary>Records a successfully executed native INSERT command against precisely the transaction captured before save.</summary>
    internal void Inserted(DbContext current, DbTransaction? actual)
    {
        Assert.Same(database, current);
        Assert.Same(transaction, actual);
        Inserts++;
    }

    /// <summary>Requires actual INSERT/save completion and an independent committed-code observation before the selected fault can fire.</summary>
    internal async ValueTask SavedAsync(DbContext current, string identifier)
    {
        Assert.Same(database, current);
        Assert.Equal(1, Inserts);
        Assert.False(string.IsNullOrEmpty(identifier));
        AccessId = identifier;
        Saves++;
        Task witness;
        using (ExecutionContext.SuppressFlow())
        {
            witness = Task.Run(RequireConsumed ?? throw new InvalidOperationException(FailureMessage));
        }
        await witness;
        if (mode == PilotGenerationFailure.AfterInsert)
        {
            ThrowFailure();
        }
    }

    /// <summary>Observes the real provider commit attempt only for the context that successfully saved the native access entry.</summary>
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction actual, TransactionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Armed && ReferenceEquals(eventData.Context, database))
        {
            Assert.Same(transaction, actual);
            Assert.Equal(transactionId, eventData.TransactionId);
            Assert.Equal(1, Saves);
            CommitAttempts++;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Loses acknowledgement only after the provider has actually committed the captured generation transaction.</summary>
    public override Task TransactionCommittedAsync(DbTransaction actual, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Armed && ReferenceEquals(eventData.Context, database))
        {
            Assert.Same(transaction, actual);
            Assert.Equal(transactionId, eventData.TransactionId);
            Assert.Equal(1, CommitAttempts);
            Commits++;
            if (mode == PilotGenerationFailure.CommitAcknowledgement)
            {
                ThrowFailure();
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>Proves actual context disposal before the only disposal fault; rollback and acknowledgement modes still retain cleanup evidence.</summary>
    private void AfterDisposal(DbContext current)
    {
        Assert.IsType<ObjectDisposedException>(Record.Exception(() => _ = current.Model));
        Disposals++;
        if (mode == PilotGenerationFailure.Disposal)
        {
            Assert.Equal(1, Commits);
            ThrowFailure();
        }
    }

    /// <summary>Records exactly one deliberately reached failure rather than accepting unrelated transport errors as evidence.</summary>
    private void ThrowFailure()
    {
        Failures++;
        throw Failure;
    }
}

/// <summary>Runs after the scoped native context has actually disposed, without introducing a production test seam.</summary>
internal sealed class PilotGenerationCleanup : IAsyncDisposable
{
    internal Action? Complete
    {
        get; set;
    }

    /// <summary>Invokes only the callback attached by the actual native generation save.</summary>
    public ValueTask DisposeAsync()
    {
        Complete?.Invoke();
        return ValueTask.CompletedTask;
    }
}
