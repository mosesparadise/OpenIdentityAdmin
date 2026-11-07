using System.Data.Common;
using IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Reaches expiry only after genuine validation cleanup, native access persistence or generation cleanup, retaining exact native ownership.</summary>
internal sealed class PilotDeadlineProbe(PilotDeadlineClock clock, bool requestExpiry) : DbTransactionInterceptor
{
    private const int BeforeGenerationSeconds = 8;
    private const int RequestSeconds = 10;
    private const int GenerationSeconds = 5;
    private const int GeneratingGate = 2;
    private DbContext? validation;
    private DbContext? generation;
    private DbTransaction? generationTransaction;
    private Guid generationId;
    private ProtocolStorageRequest? request;
    private long generationStarted;
    internal bool Armed
    {
        get; set;
    }
    internal int Gates
    {
        get; private set;
    }
    internal int ValidationCommits
    {
        get; private set;
    }
    internal int ValidationDisposals
    {
        get; private set;
    }
    internal int ValidationReturns
    {
        get; private set;
    }
    internal int Inserts
    {
        get; private set;
    }
    internal int Saves
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
    internal int Boundaries
    {
        get; private set;
    }
    internal string AccessId { get; private set; } = string.Empty;
    internal Func<Task>? RequireConsumed
    {
        get; set;
    }

    /// <summary>Installs only test observers; the real native coordinator, provider statements, watchdog timers and UTC clock remain active.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<PilotDeadlineCleanup>();
        services.AddDbContext<PlatformDbContext>((provider, options) => options.AddInterceptors(this,
            new PilotDeadlineNative(this, provider.GetRequiredService<PilotDeadlineCleanup>())));
        services.AddOpenIddict().AddServer().AddEventHandler<OpenIddictServerEvents.ValidateTokenContext>(builder => builder
            .SetOrder(OpenIddictServerHandlers.Protection.ValidateTokenEntry.Descriptor.Order + 1).UseInlineHandler(ValidatedAsync));
    }

    /// <summary>Captures each successful singleton acquisition and attaches a sentinel that runs only after its actual context has disposed.</summary>
    internal void Acquired(DbContext database, DbTransaction transaction, PilotDeadlineCleanup cleanup)
    {
        Gates++;
        Assert.InRange(Gates, 1, GeneratingGate);
        Assert.Same(transaction, database.Database.CurrentTransaction!.GetDbTransaction());
        cleanup.Complete = () => Disposed(database);
        if (Gates == 1)
        {
            validation = database;
        }
        else
        {
            Assert.Equal(1, ValidationReturns);
            Assert.NotSame(validation, database);
            generation = database;
            generationTransaction = transaction;
            generationId = database.Database.CurrentTransaction!.TransactionId;
            generationStarted = clock.GetTimestamp();
        }
    }

    /// <summary>The native validation wrapper has returned only after its short gate transaction and scope cleanup completed.</summary>
    private ValueTask ValidatedAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        if (!Armed || !PilotTokenValidation.Candidate(context))
        {
            return ValueTask.CompletedTask;
        }
        Assert.False(context.IsRejected);
        Assert.Equal(1, Gates);
        Assert.Equal(1, ValidationCommits);
        Assert.Equal(1, ValidationDisposals);
        Assert.Null(PilotStorageFence.Current);
        Assert.Equal(0, ValidationReturns++);
        request = PilotExchange.Current!.Request;
        Assert.Equal(TimeSpan.FromSeconds(RequestSeconds), request.Remaining);
        if (requestExpiry)
        {
            clock.AdvanceTo(BeforeGenerationSeconds);
            Assert.Equal(TimeSpan.FromSeconds(RequestSeconds - BeforeGenerationSeconds), request.Remaining);
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Records an actual provider INSERT only while the exact generation context is saving its new access entry.</summary>
    internal void Inserted(DbContext database, DbTransaction? transaction)
    {
        Assert.Same(generation, database);
        Assert.Same(generationTransaction, transaction);
        Inserts++;
    }

    /// <summary>Observes committed early redemption independently before advancing the original generation timeline to exact equality.</summary>
    internal async Task SavedAsync(DbContext database, string id)
    {
        Assert.Same(generation, database);
        Assert.Equal(generationId, database.Database.CurrentTransaction!.TransactionId);
        Assert.Equal(1, Inserts);
        Assert.Equal(0, Saves++);
        Assert.False(string.IsNullOrEmpty(id));
        AccessId = id;
        Task witness;
        using (ExecutionContext.SuppressFlow())
        {
            witness = Task.Run(RequireConsumed!);
        }
        await witness;
        if (!requestExpiry)
        {
            clock.AdvanceTo(GenerationSeconds);
            Assert.Equal(TimeSpan.FromSeconds(GenerationSeconds), clock.GetElapsedTime(generationStarted));
            Assert.Equal(TimeSpan.FromSeconds(RequestSeconds - GenerationSeconds), request!.Remaining);
            Assert.False(request.Token.IsCancellationRequested);
            Boundaries++;
        }
    }

    /// <summary>Counts only successful actual commits, distinguishing the initial validation and generation transactions.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Armed && ReferenceEquals(eventData.Context, validation))
        {
            ValidationCommits++;
        }
        if (Armed && ReferenceEquals(eventData.Context, generation))
        {
            Assert.Same(generationTransaction, transaction);
            Assert.Equal(generationId, eventData.TransactionId);
            Assert.Equal(1, Saves);
            Commits++;
        }
        return Task.CompletedTask;
    }

    /// <summary>Expires the original request only after actual successful generation commit and context disposal; its generation phase remains below five seconds.</summary>
    private void Disposed(DbContext database)
    {
        Assert.IsType<ObjectDisposedException>(Record.Exception(() => _ = database.Model));
        if (ReferenceEquals(database, validation))
        {
            ValidationDisposals++;
            return;
        }
        Assert.Same(generation, database);
        Disposals++;
        if (requestExpiry)
        {
            Assert.Equal(1, Commits);
            Assert.Equal(1, ValidationReturns);
            clock.AdvanceTo(RequestSeconds);
            Assert.Equal(TimeSpan.Zero, request!.Remaining);
            Assert.Equal(TimeSpan.FromSeconds(RequestSeconds - BeforeGenerationSeconds), clock.GetElapsedTime(generationStarted));
            Assert.True(clock.GetElapsedTime(generationStarted) < TimeSpan.FromSeconds(GenerationSeconds));
            Assert.False(request.Token.IsCancellationRequested);
            Boundaries++;
        }
    }
}

/// <summary>Resolved before its native context so reverse DI disposal proves actual context cleanup before the deadline observation.</summary>
internal sealed class PilotDeadlineCleanup : IAsyncDisposable
{
    internal Action? Complete
    {
        get; set;
    }

    /// <summary>Invokes only the callback attached by a real singleton gate acquisition.</summary>
    public ValueTask DisposeAsync()
    {
        Complete?.Invoke();
        return ValueTask.CompletedTask;
    }
}
