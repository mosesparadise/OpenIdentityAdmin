using System.Data.Common;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Observes exact issuance and mutation contexts at native lock/commit boundaries without replacing provider execution.</summary>
internal sealed class NativeAuthorityRaceProbe(bool issuanceFirst, string lockTable) : DbCommandInterceptor, IDbTransactionInterceptor
{
    private const int BeforeAdmission = 250;
    private const string Update = "UPDATE";
    private PlatformDbContext? issuance;
    private PlatformDbContext? mutation;
    private int issuanceCommits;
    private int mutationCommits;
    internal OpenIddictServerEndpointType? Endpoint
    {
        get; set;
    }
    internal TaskCompletionSource Captured { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ContinueIssuance { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<int> Committing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<int> PeerLock { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ReleaseCommit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Guid? IssuanceTransaction
    {
        get; private set;
    }
    internal Guid? MutationTransaction
    {
        get; private set;
    }

    /// <summary>Installs test observation after real consumption/decision but before the production issuance transaction opens.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));
        services.AddOpenIddict().AddServer().AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder => builder
            .SetOrder(OpenIddictServerHandlers.RestoreInternalClaims.Descriptor.Order + BeforeAdmission).UseInlineHandler(CaptureAsync));
    }

    /// <summary>Targets the independently scoped authoritative mutation before it begins any database work.</summary>
    internal void ArmMutation(PlatformDbContext database) => mutation = database;

    /// <summary>Captures one exact child context, excluding all earlier durable native consumption epochs.</summary>
    private async ValueTask CaptureAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (Endpoint != context.EndpointType)
        {
            return;
        }
        Assert.Null(issuance);
        Assert.NotNull(AdministratorIssuance.Find(context.Transaction));
        var owner = ProtocolStorageAmbient.Current!.Scope.Owner;
        Assert.Null(owner.Transaction);
        issuance = Assert.IsType<PlatformDbContext>(owner.Database);
        Captured.TrySetResult();
        await ContinueIssuance.Task.WaitAsync(context.CancellationToken);
    }

    /// <summary>Captures the pending peer's exact native session immediately before the actual shared-lock UPDATE.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var peer = issuanceFirst ? mutation : issuance;
        if (peer is not null && ReferenceEquals(peer, data.Context) && data.Context.Database.CurrentTransaction is not null &&
            command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(lockTable, StringComparison.Ordinal))
        {
            PeerLock.TrySetResult(NativeLockObservation.Session(command.Connection!));
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Holds the winner only after its final product checks and writes, immediately before the real provider commit.</summary>
    public async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData data,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(data.Context, issuance))
        {
            Assert.Null(IssuanceTransaction);
            IssuanceTransaction = data.TransactionId;
        }
        if (ReferenceEquals(data.Context, mutation))
        {
            Assert.Null(MutationTransaction);
            MutationTransaction = data.TransactionId;
        }
        var winner = issuanceFirst ? issuance : mutation;
        if (winner is not null && ReferenceEquals(winner, data.Context))
        {
            Assert.False(Committing.Task.IsCompleted);
            Committing.TrySetResult(NativeLockObservation.Session(transaction.Connection!));
            await ReleaseCommit.Task.WaitAsync(cancellationToken);
        }
        return result;
    }

    /// <summary>Records actual successful native commit callbacks independently of HTTP status or task arrival order.</summary>
    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(data.Context, issuance))
        {
            Assert.Equal(IssuanceTransaction, data.TransactionId);
            Interlocked.Increment(ref issuanceCommits);
        }
        if (ReferenceEquals(data.Context, mutation))
        {
            Assert.Equal(MutationTransaction, data.TransactionId);
            Interlocked.Increment(ref mutationCommits);
        }
        return Task.CompletedTask;
    }

    /// <summary>Requires distinct committed transactions, or a mutation commit with no denied issuance commit.</summary>
    internal void RequireCommits()
    {
        Assert.Equal(1, mutationCommits);
        Assert.NotNull(MutationTransaction);
        Assert.Equal(issuanceFirst ? 1 : 0, issuanceCommits);
        if (issuanceFirst)
        {
            Assert.NotNull(IssuanceTransaction);
            Assert.NotEqual(MutationTransaction, IssuanceTransaction);
        }
        else
        {
            Assert.Null(IssuanceTransaction);
        }
    }
}
