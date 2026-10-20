using System.Data.Common;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Targets only the real issuance child after durable consumption and observes its native generated artifacts and commit boundary.</summary>
internal sealed class NativeIssuanceCompletionFault(bool authorization, NativeIssuanceFailureMode mode, CancellationTokenSource cancellation) : DbTransactionInterceptor
{
    private const int BeforeAdmission = 250;
    private const string FailureMessage = "Synthetic issuance completion failure.";
    private PlatformDbContext? database;
    private Guid? transactionId;
    internal bool Armed
    {
        get; set;
    }
    internal int Attempts
    {
        get; private set;
    }
    internal int Commits
    {
        get; private set;
    }
    internal int Faults
    {
        get; private set;
    }
    internal Exception Failure
    {
        get;
    } = mode == NativeIssuanceFailureMode.Cancellation ?
        new OperationCanceledException(cancellation.Token) : new InvalidOperationException(FailureMessage);
    internal Dictionary<string, NativeIssuedArtifact> Artifacts { get; } = new(StringComparer.Ordinal);

    /// <summary>Preserves every native handler while observing the actual child before its issuance transaction opens.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));
        var server = services.AddOpenIddict().AddServer();
        server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder => builder
            .SetOrder(OpenIddictServerHandlers.RestoreInternalClaims.Descriptor.Order + BeforeAdmission).UseInlineHandler(context =>
            {
                if (Armed && context.EndpointType == (authorization ? OpenIddictServerEndpointType.Authorization : OpenIddictServerEndpointType.Token))
                {
                    Assert.Null(database);
                    Assert.NotNull(AdministratorIssuance.Find(context.Transaction));
                    var owner = ProtocolStorageAmbient.Current!.Scope.Owner;
                    Assert.Null(owner.Transaction);
                    database = Assert.IsType<PlatformDbContext>(owner.Database);
                }
                return ValueTask.CompletedTask;
            }));
        server.AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(builder => builder
            .SetOrder(OpenIddictServerHandlers.Protection.AttachTokenPayload.Descriptor.Order + 1).UseInlineHandler(ObserveAsync));
    }

    /// <summary>Retains actual native protected/reference output only when generation belongs to the captured issuance context.</summary>
    private ValueTask ObserveAsync(OpenIddictServerEvents.GenerateTokenContext context)
    {
        if (Armed && database is not null && ReferenceEquals(ProtocolStorageAmbient.Current?.Scope.Owner.Database, database))
        {
            Assert.False(string.IsNullOrEmpty(context.Token));
            Assert.False(string.IsNullOrEmpty(context.Principal.GetTokenId()));
            Assert.False(string.IsNullOrEmpty(context.Principal.GetAuthorizationId()));
            Assert.True(Artifacts.TryAdd(context.TokenType, new(context.Token, context.Principal.GetTokenId()!, context.Principal.GetAuthorizationId()!)));
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Injects one failure or explicit original-request cancellation immediately before the provider commit.</summary>
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Armed && database is not null && ReferenceEquals(eventData.Context, database))
        {
            Assert.Null(transactionId);
            transactionId = eventData.TransactionId;
            Attempts++;
            if (mode != NativeIssuanceFailureMode.CommitObservation)
            {
                if (mode == NativeIssuanceFailureMode.Cancellation)
                {
                    cancellation.Cancel();
                    Assert.True(cancellationToken.IsCancellationRequested);
                }
                Faults++;
                throw Failure;
            }
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Injects observation loss only after the actual provider has committed the same captured issuance transaction.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Armed && database is not null && ReferenceEquals(eventData.Context, database))
        {
            Assert.Equal(transactionId, eventData.TransactionId);
            Assert.Equal(NativeIssuanceFailureMode.CommitObservation, mode);
            Commits++;
            Faults++;
            throw Failure;
        }
        return Task.CompletedTask;
    }

    /// <summary>Requires the exact injected exception through production wrapping and rejects unrelated aggregate siblings.</summary>
    internal bool Contains(Exception? error) => ReferenceEquals(error, Failure) ||
        (error is AggregateException aggregate ? aggregate.InnerExceptions.Count != 0 && aggregate.InnerExceptions.All(Contains) : error?.InnerException is { } inner && Contains(inner));
}

/// <summary>Names the three independently reached issuance failure boundaries without extending production deadlines.</summary>
public enum NativeIssuanceFailureMode
{
    BeforeCommit, Cancellation, CommitObservation
}

/// <summary>Keeps synthetic artifacts private without record formatting that could disclose protected values on assertion failures.</summary>
internal sealed class NativeIssuedArtifact(string value, string identifier, string authorization)
{
    internal string Value => value;
    internal string Identifier => identifier;
    internal string Authorization => authorization;
}
