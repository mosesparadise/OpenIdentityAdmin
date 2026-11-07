using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.AspNetCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Names the real native phase held while the generation owner's physical backend is terminated.</summary>
public enum PilotGateLossStage
{
    AfterConsumption, BeforeConsumption, BeforeRevokedConsumption, DuringConsumption
}

/// <summary>Pauses one real continuation and records exact gate ownership without substituting a product transaction or handler.</summary>
internal sealed class PilotGateLossProbe(PilotGateLossStage stage) : DbCommandInterceptor
{
    private const string Pilot = "identityplatform-pilot";
    private const string Update = "UPDATE";
    private const string Gate = "PilotProtocolGate";
    private const string Tokens = "OpenIddictTokens";
    private const string StagedValue = "synthetic-unpublished-output";
    private const string DistinctSqlConnection = "The contender must own a different physical SQL connection after backend termination.";
    private const int WaitSeconds = 3;
    private readonly TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<int> bulk = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DbContext? child;
    private readonly TaskCompletionSource acquired = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DbContext? owner;
    private DbTransaction? transaction;
    private Guid sqlConnectionId;
    private int paused;
    internal bool Armed
    {
        get; set;
    }
    internal int Session
    {
        get; private set;
    }
    internal int ChildSession
    {
        get; private set;
    }

    /// <summary>Observes actual commands and inserts only a test-owned native sign-in observation.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this, new PilotGateLossSaveObservation(this, stage)));
        services.AddOpenIddict().AddServer().AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder => builder
            .SetOrder(OpenIddictServerHandlers.RestoreInternalClaims.Descriptor.Order + 1).UseInlineHandler(context =>
            {
                if (Candidate(context) && Volatile.Read(ref paused) == 0)
                {
                    context.Transaction.GetHttpRequest()!.HttpContext.Response.Headers[PilotGenerationFault.StagedHeader] = StagedValue;
                }
                return ValueTask.CompletedTask;
            })).AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder => builder
            .SetOrder(OpenIddictServerHandlers.RedeemTokenEntry.Descriptor.Order + 1).UseInlineHandler(async context =>
            {
                if (stage == PilotGateLossStage.AfterConsumption && Candidate(context) && Interlocked.CompareExchange(ref paused, 1, 0) == 0)
                {
                    await PauseAsync();
                }
            }));
    }

    /// <summary>Retains the generation owner before the barrier and independently observes another owner acquiring its released gate.</summary>
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (Armed && IsUpdate(command, Gate))
            {
                Assert.Equal(1, result);
                if (Volatile.Read(ref paused) == 0)
                {
                    owner = eventData.Context;
                    transaction = command.Transaction;
                    Session = NativeLockObservation.Session(command.Connection!);
                    sqlConnectionId = command.Connection is SqlConnection sql ? sql.ClientConnectionId : Guid.Empty;
                }
                else
                {
                    RequireNewOwner(command, eventData);
                    acquired.TrySetResult();
                }
            }
        }
        catch (Exception error)
        {
            ReportFailure(error);
            throw;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Distinguishes a new physical SQL connection even when SQL Server recycles the terminated session number.</summary>
    private void RequireNewOwner(DbCommand command, CommandExecutedEventData eventData)
    {
        Assert.NotNull(eventData.Context);
        Assert.NotSame(owner, eventData.Context);
        Assert.NotSame(transaction, command.Transaction);
        Assert.True(NativeLockObservation.Session(command.Connection!) > 0);
        if (command.Connection is SqlConnection sql)
        {
            Assert.NotEqual(Guid.Empty, sqlConnectionId);
            Assert.NotEqual(Guid.Empty, sql.ClientConnectionId);
            Assert.True(sqlConnectionId != sql.ClientConnectionId, DistinctSqlConnection);
        }
        else
        {
            Assert.NotEqual(Session, NativeLockObservation.Session(command.Connection!));
        }
    }

    /// <summary>Holds the separate consumption child before its conditional Valid-only native UPDATE can acquire the code lock.</summary>
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && (stage is PilotGateLossStage.BeforeConsumption or PilotGateLossStage.BeforeRevokedConsumption) && IsUpdate(command, Tokens) && Interlocked.CompareExchange(ref paused, 1, 0) == 0)
        {
            Assert.NotSame(owner, eventData.Context);
            Assert.NotSame(transaction, command.Transaction);
            ChildSession = NativeLockObservation.Session(command.Connection!);
            Assert.NotEqual(Session, ChildSession);
            await PauseAsync();
        }
        if (Armed && stage == PilotGateLossStage.DuringConsumption && Volatile.Read(ref paused) == 1 &&
            !ReferenceEquals(child, eventData.Context) && !ReferenceEquals(owner, eventData.Context) && IsUpdate(command, Tokens))
        {
            bulk.TrySetResult(NativeLockObservation.Session(command.Connection!));
        }
        return result;
    }

    /// <summary>Freezes the exact separate child after its fresh Valid check, immediately before native tracked redemption is saved.</summary>
    internal async Task HoldChildAsync(DbContext database)
    {
        Assert.Equal(0, Interlocked.CompareExchange(ref paused, 1, 0));
        child = database;
        Assert.NotSame(owner, child);
        Assert.NotNull(child.Database.CurrentTransaction);
        Assert.NotSame(transaction, child.Database.CurrentTransaction.GetDbTransaction());
        ChildSession = NativeLockObservation.Session(child.Database.GetDbConnection());
        Assert.NotEqual(Session, ChildSession);
        await PauseAsync();
    }

    /// <summary>Observes native bulk-revocation wait or its completed response before releasing the tracked native save.</summary>
    internal async Task<bool> ObserveBulkAsync(string provider, string connection, Task stop, CancellationToken token)
    {
        var session = await bulk.Task.WaitAsync(TimeSpan.FromSeconds(WaitSeconds), token);
        Assert.NotEqual(ChildSession, session);
        return await new NativeLockObservation(provider, connection).ObserveUntilAsync(session, ChildSession, stop, token);
    }

    /// <summary>Requires exact still-attached generation ownership before exposing the paused continuation to the test.</summary>
    private async Task PauseAsync()
    {
        Assert.NotNull(owner);
        Assert.NotNull(transaction);
        Assert.NotNull(owner.Database.CurrentTransaction);
        Assert.Same(transaction, owner.Database.CurrentTransaction.GetDbTransaction());
        Assert.True(Session > 0);
        held.TrySetResult();
        await release.Task.WaitAsync(TimeSpan.FromSeconds(WaitSeconds), TestContext.Current.CancellationToken);
    }

    /// <summary>Preserves the original observer failure instead of letting the production error boundary turn it into a barrier timeout.</summary>
    internal void ReportFailure(Exception error)
    {
        held.TrySetException(error);
        acquired.TrySetException(error);
    }

    /// <summary>Waits for the actual native barrier within the original production transaction budget.</summary>
    internal Task WaitAsync() => held.Task.WaitAsync(TimeSpan.FromSeconds(WaitSeconds), TestContext.Current.CancellationToken);

    /// <summary>Requires a successful real gate UPDATE from the competing request after backend termination.</summary>
    internal Task RequireContenderAsync() => acquired.Task.WaitAsync(TimeSpan.FromSeconds(WaitSeconds), TestContext.Current.CancellationToken);

    /// <summary>Releases the held request on every success or assertion-failure path.</summary>
    internal void Release() => release.TrySetResult();

    /// <summary>Filters only the real pilot code exchange and leaves unrelated native ceremonies untouched.</summary>
    private bool Candidate(OpenIddictServerEvents.ProcessSignInContext context) => Armed && context.ClientId == Pilot &&
        context.EndpointType == OpenIddictServerEndpointType.Token && context.Request.IsAuthorizationCodeGrantType();

    /// <summary>Recognizes a transaction-bound statement without inspecting or emitting protected command parameters.</summary>
    private static bool IsUpdate(DbCommand command, string table) => command.Transaction is not null &&
        command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains(table, StringComparison.Ordinal);
}
