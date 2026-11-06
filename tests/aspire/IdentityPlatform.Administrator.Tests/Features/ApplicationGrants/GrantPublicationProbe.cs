using System.Data.Common;
using System.Diagnostics;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Observes one actual grant commit and its exact provider transaction disposal without adding disposable services to native scopes.</summary>
internal sealed class GrantPublicationProbe : GrantCleanupProbe
{
    internal bool Armed
    {
        get; set;
    }
    internal int Commits
    {
        get; private set;
    }

    /// <summary>Uses the real provider interceptor chain without changing context or scope construction.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Binds only a genuinely committed grant transaction; setup and explicit recovery remain unaffected.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Armed && eventData.Context is { } database && database.ChangeTracker.Entries<ApplicationGrantApproval>().Any())
        {
            Armed = false;
            Commits++;
            Bind(database, eventData.TransactionId, transaction);
        }
        return Task.CompletedTask;
    }
}

/// <summary>Gates the exact real provider transaction cleanup, then independently checks final context disposal after the HTTP operation completes.</summary>
internal abstract class GrantCleanupProbe : DbTransactionInterceptor, IDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private const string Listener = "Microsoft.EntityFrameworkCore";
    private const string DisposedEvent = "Microsoft.EntityFrameworkCore.Database.Transaction.TransactionDisposed";
    private const string DiagnosticFailure = "Native cleanup diagnostic observation failed.";
    private readonly List<IDisposable> listeners = [];
    private readonly IDisposable subscription;

    /// <summary>Observes only the documented EF transaction-disposed event, including existing native listeners, for this test's lifetime.</summary>
    protected GrantCleanupProbe() => subscription = DiagnosticListener.AllListeners.Subscribe(this);

    /// <summary>Filters the native diagnostic stream before any payload is observed; unrelated activity is neither retained nor delayed.</summary>
    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == Listener)
        {
            lock (listeners)
            {
                listeners.Add(listener.Subscribe((IObserver<KeyValuePair<string, object?>>)this, name => name == DisposedEvent));
            }
        }
    }

    /// <summary>Accepts only the documented provider transaction event and delegates exact reference/context binding to the cleanup observer.</summary>
    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Key == DisposedEvent && value.Value is TransactionEventData data)
        {
            TransactionDisposed(data.Transaction, data);
        }
    }

    /// <summary>A completed listener needs no action; explicit test disposal releases every retained subscription.</summary>
    public void OnCompleted()
    {
    }

    /// <summary>Fails an unexpected diagnostic-stream error instead of treating missing observation as acceptance.</summary>
    public void OnError(Exception error) => throw new InvalidOperationException(DiagnosticFailure, error);

    /// <summary>Stops listener discovery and releases all test-owned event subscriptions after the fixture and HTTP work have drained.</summary>
    public void Dispose()
    {
        subscription.Dispose();
        lock (listeners)
        {
            foreach (var listener in listeners)
            {
                listener.Dispose();
            }
            listeners.Clear();
        }
    }

    private DbContext? database;
    private DbTransaction? transaction;
    private Guid transactionId;
    internal int Disposals
    {
        get; private set;
    }
    internal Action AfterDisposal { get; set; } = () => { };

    /// <summary>Captures actual context and transaction identity once, never a simulated completion flag.</summary>
    internal void Bind(DbContext context, Guid id, DbTransaction native)
    {
        Assert.Null(database);
        database = context;
        transactionId = id;
        transaction = native;
    }

    /// <summary>Runs only after EF has disposed the exact underlying provider transaction; the bounded test gate remains part of awaited production cleanup.</summary>
    private void TransactionDisposed(DbTransaction native, TransactionEventData eventData)
    {
        if (!ReferenceEquals(native, transaction))
        {
            return;
        }
        Assert.Same(database, eventData.Context);
        Assert.Equal(transactionId, eventData.TransactionId);
        Disposals++;
        Assert.Equal(1, Disposals);
        AfterDisposal();
    }

    /// <summary>Requires actual native context disposal after release/join, before accepting the HTTP result as evidence.</summary>
    internal void RequireContextDisposed()
    {
        Assert.NotNull(database);
        Assert.IsType<ObjectDisposedException>(Record.Exception(() => _ = database.Model));
    }
}
